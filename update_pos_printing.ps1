$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Infrastructure\Printing"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\pos\utils"

# 1. Backend EscPosPrintService (TCP Raw socket)
@"
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Printing;

public interface IPrintService
{
    Task PrintReceiptAsync(string printerIp, int port, string textContent);
}

public class EscPosPrintService : IPrintService
{
    public async Task PrintReceiptAsync(string printerIp, int port, string textContent)
    {
        try
        {
            using var client = new TcpClient();
            // Timeout after 3 seconds so we don't block POS billing
            var connectTask = client.ConnectAsync(printerIp, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
            {
                throw new TimeoutException($"Could not connect to printer at {printerIp}:{port}");
            }

            using var stream = client.GetStream();
            
            // Basic ESC/POS initialization: ESC @
            byte[] initCommand = new byte[] { 0x1B, 0x40 };
            await stream.WriteAsync(initCommand, 0, initCommand.Length);

            // Write receipt text
            byte[] textBytes = Encoding.ASCII.GetBytes(textContent);
            await stream.WriteAsync(textBytes, 0, textBytes.Length);

            // Feed and cut paper: GS V 0
            byte[] cutCommand = new byte[] { 0x0A, 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00 };
            await stream.WriteAsync(cutCommand, 0, cutCommand.Length);
            
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            // Log printer failure
            Console.WriteLine($"Printer Error: {ex.Message}");
            throw;
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Printing\EscPosPrintService.cs" -Encoding utf8

# 2. Add Print Endpoint to PosController
@"
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Infrastructure.Printing;
using PosErp.Domain.Entities.Pos;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPrintService _printService;

    public PosController(IMediator mediator, IPrintService printService)
    {
        _mediator = mediator;
        _printService = printService;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncInvoicesCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
    
    [HttpPost("print")]
    public async Task<IActionResult> PrintReceipt([FromBody] PrintRequest request)
    {
        // Convert invoice JSON to plain text ESC/POS string layout here
        string receiptText = GenerateReceiptText(request.Invoice);
        
        // E.g., IP fetched from Terminal config
        await _printService.PrintReceiptAsync("192.168.1.100", 9100, receiptText);
        return Ok();
    }

    private string GenerateReceiptText(OfflineInvoiceDto invoice)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("     ENTERPRISE SUPERMARKET");
        sb.AppendLine("          Tax Invoice");
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"Inv: {invoice.InvoiceNumber}");
        sb.AppendLine($"Date: {invoice.BusinessDate}");
        sb.AppendLine("--------------------------------");
        foreach(var item in invoice.Items)
        {
            sb.AppendLine($"{item.ProductName}");
            sb.AppendLine($"{item.Quantity} x {item.UnitPrice}    {item.TotalAmount}");
        }
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"TOTAL:           {invoice.TotalAmount}");
        sb.AppendLine($"NET PAYABLE:     {invoice.NetPayable}");
        sb.AppendLine("--------------------------------");
        sb.AppendLine("   Thank you for shopping!   ");
        return sb.ToString();
    }
}

public class PrintRequest
{
    public OfflineInvoiceDto Invoice { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Controllers\PosController.cs" -Encoding utf8

# 3. Frontend Web Serial / Proxy Printer Utility
@"
import { api } from '@/utils/api';
import { Invoice } from '../types';

/**
 * Converts the invoice object into a raw text string suitable for thermal printers.
 */
const generateEscPosText = (invoice: Invoice): string => {
  let text = "     ENTERPRISE SUPERMARKET\n";
  text += "          Tax Invoice\n";
  text += "--------------------------------\n";
  text += \`Inv: \${invoice.invoiceNumber}\n\`;
  text += \`Date: \${new Date(invoice.businessDate).toLocaleString()}\n\`;
  text += "--------------------------------\n";
  invoice.items.forEach(item => {
    text += \`\${item.name}\n\`;
    text += \`\${item.quantity} x \${item.unitPrice.toFixed(2)}    \${item.totalAmount.toFixed(2)}\n\`;
  });
  text += "--------------------------------\n";
  text += \`TOTAL:           \${invoice.totalAmount.toFixed(2)}\n\`;
  text += \`NET PAYABLE:     \${invoice.netPayable.toFixed(2)}\n\`;
  text += "--------------------------------\n";
  text += "   Thank you for shopping!   \n\n\n\n";
  return text;
};

/**
 * Attempts to print using Web Serial API (USB ESC/POS).
 * Falls back to Network Printer Proxy via Backend.
 */
export const printReceipt = async (invoice: Invoice, useWebSerial: boolean = true) => {
  const textContent = generateEscPosText(invoice);

  // 1. Web Serial API (Hardware USB)
  if (useWebSerial && 'serial' in navigator) {
    try {
      // @ts-ignore
      const port = await navigator.serial.requestPort();
      await port.open({ baudRate: 9600 });
      const writer = port.writable.getWriter();
      
      const encoder = new TextEncoder();
      
      // Init ESC/POS
      await writer.write(new Uint8Array([0x1B, 0x40]));
      // Write text
      await writer.write(encoder.encode(textContent));
      // Cut paper
      await writer.write(new Uint8Array([0x0A, 0x0A, 0x0A, 0x0A, 0x1D, 0x56, 0x00]));
      
      writer.releaseLock();
      await port.close();
      return true;
    } catch (err) {
      console.warn("Web Serial failed or user cancelled. Falling back to Network Proxy.", err);
    }
  }

  // 2. Fallback: Network Proxy (LAN Printer via Backend)
  try {
    await api.post('/api/pos/print', { invoice });
    return true;
  } catch (err) {
    console.error("Network Print Proxy failed.", err);
    
    // 3. Last Resort: CSS window.print() 
    window.print();
    return false;
  }
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\utils\printer.ts" -Encoding utf8

# 4. Update PosTerminal.tsx to use printReceipt instead of window.print
$terminalFile = "$frontendDir\src\features\pos\components\PosTerminal.tsx"
$content = Get-Content -Path $terminalFile -Raw
$content = $content -replace "window\.print\(\);", "await printReceipt(invoice);"
$content = "import { printReceipt } from '../utils/printer';`n" + $content
$content | Out-File -FilePath $terminalFile -Encoding utf8

Write-Host "POS Printing Updated"
