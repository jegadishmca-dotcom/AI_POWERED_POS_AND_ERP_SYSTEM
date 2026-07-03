using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Commands;

public record ProcessInterStoreTransferCommand(
    Guid FromStoreId,
    Guid ToStoreId,
    DateTime TransferDate,
    List<TransferItemInputDto> Items,
    Guid UserId
) : IRequest<Guid>;

public record TransferItemInputDto(
    Guid ProductId,
    Guid BatchId,
    decimal Quantity
);

public class TransferCommandsHandler : IRequestHandler<ProcessInterStoreTransferCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _postingService;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IStockLedgerService _stockLedgerService;

    public TransferCommandsHandler(
        IApplicationDbContext context,
        IFinancialPostingService postingService,
        IDocumentSequenceService sequenceService,
        IStockLedgerService stockLedgerService)
    {
        _context = context;
        _postingService = postingService;
        _sequenceService = sequenceService;
        _stockLedgerService = stockLedgerService;
    }

    public async Task<Guid> Handle(ProcessInterStoreTransferCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            if (request.FromStoreId == request.ToStoreId)
            {
                throw new InvalidOperationException("Source and destination stores cannot be the same.");
            }

            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                string transferNo = await _sequenceService.GenerateNextNumberAsync(request.FromStoreId, "INTER_STORE_TRANSFER", cancellationToken);

                var transfer = new InterStoreTransfer
                {
                    TransferNumber = transferNo,
                    FromStoreId = request.FromStoreId,
                    ToStoreId = request.ToStoreId,
                    TransferDate = request.TransferDate.Date,
                    Status = "RECEIVED", // auto-receive for integration tests
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = request.UserId
                };

                decimal totalValuation = 0;

                foreach (var item in request.Items)
                {
                    var sourceBatch = await _context.ProductBatches.FindAsync(new object[] { item.BatchId }, cancellationToken);
                    if (sourceBatch == null) throw new InvalidOperationException($"Product batch {item.BatchId} not found.");

                    if (sourceBatch.AvailableQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient stock in source batch {sourceBatch.BatchNumber}. Available: {sourceBatch.AvailableQuantity}, Requested: {item.Quantity}");
                    }

                    // Deduct from source batch
                    sourceBatch.AvailableQuantity -= item.Quantity;

                    // Restock or create batch in destination store
                    var destBatch = await _context.ProductBatches
                        .FirstOrDefaultAsync(b => b.ProductId == item.ProductId && b.BatchNumber == sourceBatch.BatchNumber && b.StoreId == request.ToStoreId, cancellationToken);
                    if (destBatch == null)
                    {
                        destBatch = new ProductBatch
                        {
                            StoreId = request.ToStoreId,
                            ProductId = item.ProductId,
                            BatchNumber = sourceBatch.BatchNumber,
                            MfgDate = sourceBatch.MfgDate,
                            ExpiryDate = sourceBatch.ExpiryDate,
                            Mrp = sourceBatch.Mrp,
                            CostPrice = sourceBatch.CostPrice,
                            AvailableQuantity = item.Quantity,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.ProductBatches.Add(destBatch);
                    }
                    else
                    {
                        destBatch.AvailableQuantity += item.Quantity;
                    }

                    // Add transfer line
                    transfer.Items.Add(new InterStoreTransferItem
                    {
                        ProductId = item.ProductId,
                        BatchId = item.BatchId,
                        Quantity = item.Quantity,
                        UnitCost = sourceBatch.CostPrice
                    });

                    // Record stock movement (out from source)
                    await _stockLedgerService.RecordMovementAsync(
                        storeId: request.FromStoreId,
                        warehouseId: null,
                        terminalId: null,
                        businessDate: request.TransferDate.Date,
                        productId: item.ProductId,
                        batchId: item.BatchId,
                        movementType: "TRANSFER_OUT",
                        quantity: -item.Quantity,
                        unitCost: sourceBatch.CostPrice,
                        expiryDate: sourceBatch.ExpiryDate,
                        referenceDocId: transfer.Id,
                        referenceNumber: transferNo,
                        userId: request.UserId,
                        cancellationToken: cancellationToken
                    );

                    // Record stock movement (in to destination)
                    // We'll update the database to save changes first to get destBatch.Id
                    await _context.SaveChangesAsync(cancellationToken);

                    await _stockLedgerService.RecordMovementAsync(
                        storeId: request.ToStoreId,
                        warehouseId: null,
                        terminalId: null,
                        businessDate: request.TransferDate.Date,
                        productId: item.ProductId,
                        batchId: destBatch.Id,
                        movementType: "TRANSFER_IN",
                        quantity: item.Quantity,
                        unitCost: sourceBatch.CostPrice,
                        expiryDate: sourceBatch.ExpiryDate,
                        referenceDocId: transfer.Id,
                        referenceNumber: transferNo,
                        userId: request.UserId,
                        cancellationToken: cancellationToken
                    );

                    totalValuation += (sourceBatch.CostPrice * item.Quantity);
                }

                _context.InterStoreTransfers.Add(transfer);
                await _context.SaveChangesAsync(cancellationToken);

                // Ensure accounting codes exist
                await EnsureAccountExistsAsync("10300", "Inventory Asset", "ASSET", cancellationToken);
                await EnsureAccountExistsAsync("10900", "Inter-Store Clearing", "ASSET", cancellationToken);

                // Store A (Source) Ledger Posting:
                // Debit Inter-Store Clearing 10900 (Receivable from B)
                // Credit Inventory Asset 10300 (Value leaves Store A)
                var sourceLines = new List<JournalLineDto>
                {
                    new() { AccountCode = "10900", Description = $"Inter-Store Transit to {request.ToStoreId}", Debit = totalValuation, Credit = 0 },
                    new() { AccountCode = "10300", Description = $"Inventory Transfer Out to {request.ToStoreId}", Debit = 0, Credit = totalValuation }
                };

                Guid srcJeId = await _postingService.PostJournalEntryWithUserAsync(
                    request.FromStoreId,
                    request.TransferDate,
                    $"Inter-Store Transfer Out {transferNo}",
                    transferNo,
                    sourceLines,
                    request.UserId,
                    isDraft: false,
                    cancellationToken,
                    sourceModule: "INVENTORY",
                    sourceDocType: "TRANSFER_OUT",
                    sourceDocId: transfer.Id
                );

                // Store B (Destination) Ledger Posting:
                // Debit Inventory Asset 10300 (Value enters Store B)
                // Credit Inter-Store Clearing 10900 (Payable to A)
                var destLines = new List<JournalLineDto>
                {
                    new() { AccountCode = "10300", Description = $"Inventory Transfer In from {request.FromStoreId}", Debit = totalValuation, Credit = 0 },
                    new() { AccountCode = "10900", Description = $"Inter-Store Clearing from {request.FromStoreId}", Debit = 0, Credit = totalValuation }
                };

                Guid destJeId = await _postingService.PostJournalEntryWithUserAsync(
                    request.ToStoreId,
                    request.TransferDate,
                    $"Inter-Store Transfer In {transferNo}",
                    transferNo,
                    destLines,
                    request.UserId,
                    isDraft: false,
                    cancellationToken,
                    sourceModule: "INVENTORY",
                    sourceDocType: "TRANSFER_IN",
                    sourceDocId: transfer.Id
                );

                transfer.JournalEntryId = srcJeId; // Link source JE as primary

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return transfer.Id;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task EnsureAccountExistsAsync(string code, string name, string type, CancellationToken cancellationToken)
    {
        var exists = await _context.Accounts.AnyAsync(a => a.AccountCode == code, cancellationToken);
        if (!exists)
        {
            _context.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                AccountCode = code,
                Name = name,
                AccountType = type,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
