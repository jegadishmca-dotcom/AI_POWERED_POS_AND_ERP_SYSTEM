using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosErp.Application.Behaviors;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Finance;
using PosErp.Infrastructure.Identity;
using PosErp.Infrastructure.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PosErp.IntegrationTests
{
    [Collection("Database Collection")]
    public class F48_ConcurrencyStressTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Guid _terminalId = Guid.NewGuid();
        private readonly Guid _cashierId = Guid.NewGuid();
        private readonly Guid _productId = Guid.NewGuid();
        private readonly Guid _batchId = Guid.NewGuid();

        public F48_ConcurrencyStressTests()
        {
            _context = IntegrationTestDbFactory.Build();
        }

        private async Task SeedDataAsync()
        {
            // Seed Terminal
            var terminal = new Terminal
            {
                Id = _terminalId,
                TerminalCode = "STRESS-T1",
                Name = "Stress Terminal",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Terminals.Add(terminal);

            // Seed Tax Slab
            var taxSlab = new TaxSlab
            {
                Id = Guid.NewGuid(),
                Name = "GST 0%",
                CgstRate = 0m,
                SgstRate = 0m,
                IgstRate = 0m,
                CessRate = 0m,
                IsDeleted = false
            };
            _context.TaxSlabs.Add(taxSlab);

            // Seed UOM
            var uom = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Pieces",
                Symbol = "PCS",
                IsDeleted = false
            };
            _context.UnitOfMeasures.Add(uom);

            // Seed Product
            var product = new Product
            {
                Id = _productId,
                ProductCode = "P-STRESS-01",
                Name = "Stress Test Product",
                TaxSlabId = taxSlab.Id,
                UnitOfMeasureId = uom.Id,
                Mrp = 10m,
                SellingPrice = 10m,
                PurchasePrice = 5m,
                IsActive = true
            };
            _context.Products.Add(product);

            // Seed Barcode
            var barcode = new Barcode
            {
                Id = Guid.NewGuid(),
                ProductId = _productId,
                BarcodeValue = "BAR-STRESS-01",
                IsPrimary = true
            };
            _context.Barcodes.Add(barcode);

            // Seed Product Batch
            var batch = new ProductBatch
            {
                Id = _batchId,
                ProductId = _productId,
                BatchNumber = "BATCH-STRESS-01",
                IsActive = true,
                MfgDate = DateTime.UtcNow.Date.AddDays(-10),
                ExpiryDate = DateTime.UtcNow.Date.AddDays(100),
                AvailableQuantity = 100000m,
                Mrp = 10m,
                CostPrice = 5m
            };
            _context.ProductBatches.Add(batch);

            // Seed Stock Ledger
            var stock = new StockLedgerEntry
            {
                Id = Guid.NewGuid(),
                StoreId = Guid.Empty,
                ProductId = _productId,
                BatchId = _batchId,
                MovementType = "GRN",
                Quantity = 100000m,
                UnitCost = 5m,
                RunningBalance = 100000m,
                BusinessDate = DateTime.UtcNow.Date,
                ReferenceDocumentId = Guid.NewGuid(),
                ReferenceNumber = "SEED-STRESS"
            };
            _context.StockLedger.Add(stock);

            // Open Store Business Date
            if (!await _context.StoreBusinessDates.AnyAsync(
                    b => b.StoreId == Guid.Empty && b.BusinessDate == DateTime.UtcNow.Date))
            {
                _context.StoreBusinessDates.Add(new StoreBusinessDate
                {
                    StoreId = Guid.Empty,
                    BusinessDate = DateTime.UtcNow.Date,
                    Status = "OPEN"
                });
            }

            // Clean up any old request records
            _context.Database.ExecuteSqlRaw("DELETE FROM idempotent_requests;");

            await _context.SaveChangesAsync();
        }

        private async Task<CreateInvoiceResponse> ExecuteCheckoutAsync(CreateInvoiceCommand cmd)
        {
            // Build fresh container context per thread to simulate multi-terminal API container instances
            using var context = IntegrationTestDbFactory.CreateNewContext();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var hasher = new PasswordHasher();
            var periodLock = new PeriodLockService(context);
            var docSeq = new DocumentSequenceService(context);
            var approval = new ApprovalWorkflowService(context);
            var posting = new FinancialPostingService(context, periodLock, docSeq, approval);
            var stockSvc = new StockLedgerService(context);
            var walletSvc = new WalletService(context);
            var loyaltySvc = new LoyaltyService(context);
            var offerEng = new OfferEngine(context, cache);
            var accountRes = new PosErp.Infrastructure.Services.AccountResolutionService(context);

            // Resolve using MediatR pipeline behaviors (Idempotency -> Retry -> Handler)
            var services = new ServiceCollection();
            services.AddSingleton<IApplicationDbContext>(context);
            services.AddSingleton<IPasswordHasher>(hasher);
            services.AddSingleton<IPeriodLockService>(periodLock);
            services.AddSingleton<IDocumentSequenceService>(docSeq);
            services.AddSingleton<IApprovalWorkflowService>(approval);
            services.AddSingleton<IFinancialPostingService>(posting);
            services.AddSingleton<IStockLedgerService>(stockSvc);
            services.AddSingleton<IWalletService>(walletSvc);
            services.AddSingleton<ILoyaltyService>(loyaltySvc);
            services.AddSingleton<IOfferEngine>(offerEng);
            services.AddSingleton<IAccountResolutionService>(accountRes);
            services.AddLogging();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(CreateInvoiceCommand).Assembly);
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransientRetryBehavior<,>));
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            return await mediator.Send(cmd);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(25)]
        [InlineData(50)]
        [InlineData(100)]
        public async Task ExecuteWorkloads(int concurrency)
        {
            await SeedDataAsync();

            var tasks = new List<Task<(CreateInvoiceResponse? Response, double DurationMs, Exception? Exception)>>();
            var startBarrier = new TaskCompletionSource();

            for (int i = 0; i < concurrency; i++)
            {
                var index = i;
                var cmd = new CreateInvoiceCommand(
                    InvoiceNumber: "", // auto generate
                    TerminalId: _terminalId,
                    CashierId: _cashierId,
                    CustomerId: null,
                    PromoCode: null,
                    WalletAmountUsed: 0m,
                    CashAmount: 10m,
                    UpiAmount: 0m,
                    CardAmount: 0m,
                    RoundOff: 0m,
                    NetPayable: 10m,
                    PaymentMode: "CASH",
                    Items: new List<InvoiceItemDto> { new(_productId, 1, 10m, _batchId) },
                    PointsRedeemed: 0,
                    SupervisorOverridePin: null,
                    ClientRequestToken: Guid.NewGuid() // Unique token
                );

                tasks.Add(Task.Run(async () =>
                {
                    await startBarrier.Task; // Synchronize start
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        var res = await ExecuteCheckoutAsync(cmd);
                        watch.Stop();
                        return (res, watch.Elapsed.TotalMilliseconds, (Exception?)null);
                    }
                    catch (Exception ex)
                    {
                        watch.Stop();
                        return ((CreateInvoiceResponse?)null, watch.Elapsed.TotalMilliseconds, ex);
                    }
                }));
            }

            // Release all tasks concurrently
            var totalWatch = Stopwatch.StartNew();
            startBarrier.SetResult();
            await Task.WhenAll(tasks);
            totalWatch.Stop();

            var results = tasks.Select(t => t.Result).ToList();
            var successCount = results.Count(r => r.Response != null);
            var failureCount = results.Count(r => r.Exception != null);
            var durations = results.Where(r => r.Response != null).Select(r => r.DurationMs).OrderBy(d => d).ToList();

            // Assertions
            Assert.Equal(concurrency, successCount); // 100% success rate expected
            Assert.Equal(0, failureCount);

            // Fetch created invoices and verify unique document sequence numbers
            using var validationContext = IntegrationTestDbFactory.CreateNewContext();
            var invoices = await validationContext.Invoices
                .Where(inv => inv.TerminalId == _terminalId)
                .ToListAsync();

            Assert.Equal(concurrency, invoices.Count);
            
            // 1. Unique Invoice Numbers
            var uniqueInvoiceNumbers = invoices.Select(inv => inv.InvoiceNumber).Distinct().Count();
            Assert.Equal(concurrency, uniqueInvoiceNumbers);

            // 2. Unique Terminal Sequences
            var uniqueTerminalSequences = invoices.Select(inv => inv.TerminalSequence).Distinct().Count();
            Assert.Equal(concurrency, uniqueTerminalSequences);

            // 3. Verify Batch stock subtraction is exact
            var finalBatch = await validationContext.ProductBatches.FindAsync(_batchId);
            Assert.NotNull(finalBatch);
            decimal expectedStock = 100000m - concurrency;
            Assert.Equal(expectedStock, finalBatch.AvailableQuantity);

            // Calculate Performance Metrics
            double totalDuration = totalWatch.Elapsed.TotalMilliseconds;
            double tps = concurrency / (totalDuration / 1000.0);
            double avgLatency = durations.Average();
            double p95 = durations[(int)(durations.Count * 0.95)];
            double p99 = durations[(int)(durations.Count * 0.99)];

            Console.WriteLine($"\n=== STRESS TEST WORKLOAD METRICS (Concurrency: {concurrency}) ===");
            Console.WriteLine($"Total Successful Checkouts: {successCount}");
            Console.WriteLine($"Total Failures: {failureCount}");
            Console.WriteLine($"Total Workload Execution Time: {totalDuration:F2} ms");
            Console.WriteLine($"Throughput (TPS): {tps:F2} TPS");
            Console.WriteLine($"Average Request Latency: {avgLatency:F2} ms");
            Console.WriteLine($"P95 Request Latency: {p95:F2} ms");
            Console.WriteLine($"P99 Request Latency: {p99:F2} ms");
            Console.WriteLine($"Final Product Batch Stock Level: {finalBatch.AvailableQuantity}");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
