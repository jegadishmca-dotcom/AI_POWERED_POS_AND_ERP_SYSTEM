$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Pos"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Pos\Commands\SyncInvoices"

# 1. POS Domain Entities
@"
using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Pos;

public class Invoice
{
    // Composite Key in DB: (Id, BusinessDate)
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public DateTime BusinessDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    
    public Guid TerminalId { get; set; }
    public int TerminalSequence { get; set; }
    public Guid CashierId { get; set; }
    
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal NetPayable { get; set; }
    
    // E-Invoicing Hooks
    public string? Irn { get; set; }
    public string? AckNo { get; set; }
    public DateTime? AckDate { get; set; }
    public string? QrCode { get; set; }
    
    public string Status { get; set; } = "COMPLETED"; // COMPLETED, CANCELLED, HOLD
    public string PaymentMode { get; set; } = "CASH"; // CASH, CARD, UPI
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public class InvoiceItem
{
    // Composite Key in DB: (Id, BusinessDate)
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime BusinessDate { get; set; }
    
    public Guid ProductId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    
    public decimal CgstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstRate { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal CessRate { get; set; }
    public decimal CessAmount { get; set; }
    
    public decimal TotalAmount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    
    public Invoice Invoice { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Pos\PosEntities.cs" -Encoding utf8

# 2. Update DbContext
@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Pos;

namespace PosErp.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<TaxSlab> TaxSlabs { get; }

    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs" -Encoding utf8


# 3. SyncInvoicesCommand
@"
using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

public record SyncInvoicesCommand(List<OfflineInvoiceDto> Invoices) : IRequest<SyncResult>;
public record SyncResult(int Synced, int Failed, List<string> Errors);

public record OfflineInvoiceDto(
    Guid Id,
    DateTime BusinessDate,
    string InvoiceNumber,
    Guid TerminalId,
    int TerminalSequence,
    Guid CashierId,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal RoundOff,
    decimal NetPayable,
    string PaymentMode,
    List<OfflineInvoiceItemDto> Items
);

public record OfflineInvoiceItemDto(
    Guid Id,
    Guid ProductId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount,
    decimal CessRate,
    decimal CessAmount,
    decimal TotalAmount
);

public class SyncInvoicesCommandHandler : IRequestHandler<SyncInvoicesCommand, SyncResult>
{
    private readonly IApplicationDbContext _context;

    public SyncInvoicesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SyncResult> Handle(SyncInvoicesCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        int synced = 0;
        int failed = 0;

        foreach (var dto in request.Invoices)
        {
            try
            {
                // Check if already synced
                var exists = _context.Invoices.Any(i => i.Id == dto.Id && i.BusinessDate == dto.BusinessDate);
                if (exists)
                {
                    synced++;
                    continue; // Skip silently
                }

                var invoice = new Invoice
                {
                    Id = dto.Id,
                    BusinessDate = dto.BusinessDate,
                    InvoiceNumber = dto.InvoiceNumber,
                    TerminalId = dto.TerminalId,
                    TerminalSequence = dto.TerminalSequence,
                    CashierId = dto.CashierId,
                    SubTotal = dto.SubTotal,
                    DiscountAmount = dto.DiscountAmount,
                    TaxAmount = dto.TaxAmount,
                    TotalAmount = dto.TotalAmount,
                    RoundOff = dto.RoundOff,
                    NetPayable = dto.NetPayable,
                    PaymentMode = dto.PaymentMode,
                    Status = "COMPLETED"
                };

                foreach (var itemDto in dto.Items)
                {
                    invoice.Items.Add(new InvoiceItem
                    {
                        Id = itemDto.Id,
                        BusinessDate = dto.BusinessDate,
                        ProductId = itemDto.ProductId,
                        Barcode = itemDto.Barcode,
                        ProductName = itemDto.ProductName,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice,
                        DiscountAmount = itemDto.DiscountAmount,
                        CgstRate = itemDto.CgstRate,
                        CgstAmount = itemDto.CgstAmount,
                        SgstRate = itemDto.SgstRate,
                        SgstAmount = itemDto.SgstAmount,
                        CessRate = itemDto.CessRate,
                        CessAmount = itemDto.CessAmount,
                        TotalAmount = itemDto.TotalAmount
                    });
                }

                _context.Invoices.Add(invoice);
                synced++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Failed to sync Invoice {dto.InvoiceNumber}: {ex.Message}");
            }
        }

        if (synced > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new SyncResult(synced, failed, errors);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Pos\Commands\SyncInvoices\SyncInvoicesCommand.cs" -Encoding utf8

# 4. PosController
@"
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncInvoicesCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
    
    // TODO: Add Catalog Snapshot endpoint here
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Controllers\PosController.cs" -Encoding utf8

Write-Host "Backend POS Scaffolded"
