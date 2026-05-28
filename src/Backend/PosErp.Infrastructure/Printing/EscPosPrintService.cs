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
            byte[] textBytes = Encoding.UTF8.GetBytes(textContent);
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
