using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Loyalty;
using PosErp.Domain.Entities.Crm;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Manager")]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;

    public LoyaltyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        return Ok(await _mediator.Send(new GetLoyaltyConfigQuery()));
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] LoyaltyProgramConfig config)
    {
        return Ok(await _mediator.Send(new UpdateLoyaltyConfigCommand(config)));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        return Ok(await _mediator.Send(new GetLoyaltyDashboardQuery()));
    }

    [HttpGet("liability-report")]
    public async Task<IActionResult> GetLiabilityReport()
    {
        return Ok(await _mediator.Send(new GetLoyaltyLiabilityReportQuery()));
    }

    [HttpPost("trigger-jobs")]
    public async Task<IActionResult> TriggerJobs([FromServices] PosErp.Application.Features.Loyalty.Jobs.ILoyaltyBackgroundJobs jobs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        await jobs.ExpirePointsJob();
        await jobs.EvaluateTierDowngradeJob();
        await jobs.BirthdayBonusJob();
        await jobs.AnniversaryBonusJob();
        await jobs.LoyaltyMaintenanceJob();
        
        sw.Stop();
        return Ok(new { message = "Jobs executed successfully", executionTimeMs = sw.ElapsedMilliseconds });
    }

    [HttpPost("seed-10k-customers")]
    public async Task<IActionResult> SeedCustomers([FromServices] PosErp.Application.Interfaces.IApplicationDbContext context)
    {
        var count = context.Customers.Count();
        if (count >= 10000) return Ok("Already seeded");

        var customers = new System.Collections.Generic.List<Customer>();
        var random = new System.Random();
        var today = System.DateTime.UtcNow.Date;

        for (int i = 0; i < 10000; i++)
        {
            var isBirthday = random.NextDouble() < 0.05; // 5% chance of birthday today
            var isAnniversary = random.NextDouble() < 0.05; // 5% chance of anniversary today
            var isExpired = random.NextDouble() < 0.1; // 10% chance to have old points
            
            var c = new Customer
            {
                Id = System.Guid.NewGuid(),
                Phone = "9" + random.Next(100000000, 999999999).ToString(),
                Name = "Perf Test Customer " + i,
                RunningLoyaltyPoints = isExpired ? 500 : random.Next(10, 5000),
                // Tier doesn't have a direct string setter anymore in some contexts but let's check
                LifetimeSpend = random.Next(0, 5000),
                Dob = isBirthday ? today.AddYears(-30) : today.AddDays(random.Next(1, 300)).AddYears(-30),
                Anniversary = isAnniversary ? today.AddYears(-5) : today.AddDays(random.Next(1, 300)).AddYears(-5)
            };
            customers.Add(c);
        }

        context.Customers.AddRange(customers);

        // Add an old ledger entry for the expired ones
        foreach (var c in customers.Where(x => x.RunningLoyaltyPoints == 500).Take(500))
        {
            context.LoyaltyLedger.Add(new LoyaltyLedgerEntry
            {
                Id = System.Guid.NewGuid(),
                CustomerId = c.Id,
                TransactionType = "Earned",
                PointsEarned = 500,
                PointsRedeemed = 0,
                PreviousBalance = 0,
                BalanceAfterTransaction = 500,
                CreatedAt = today.AddDays(-400), // very old
                CreatedBy = System.Guid.Empty
            });
        }

        await context.SaveChangesAsync(System.Threading.CancellationToken.None);
        return Ok("Seeded 10,000 customers");
    }
}
