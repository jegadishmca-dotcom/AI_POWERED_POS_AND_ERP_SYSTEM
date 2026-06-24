using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Ai.Jobs;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/ai/validate-jobs")]
public class AiJobValidationController : ControllerBase
{
    private readonly IAiBackgroundJobs _jobs;

    public AiJobValidationController(IAiBackgroundJobs jobs)
    {
        _jobs = jobs;
    }

    [HttpPost("run-all")]
    [AllowAnonymous]
    public async Task<IActionResult> RunAllJobs()
    {
        var results = new List<object>();

        async Task RunAndMeasure(string name, Func<Task> job)
        {
            var sw = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(true);
            try
            {
                await job();
                sw.Stop();
                var memoryAfter = GC.GetTotalMemory(true);
                results.Add(new
                {
                    JobName = name,
                    Status = "Success",
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    MemoryDiffBytes = memoryAfter - memoryBefore
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new
                {
                    JobName = name,
                    Status = "Failed",
                    Error = ex.Message,
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                });
            }
        }

        await RunAndMeasure("InsightGenerationJob", () => _jobs.ExecuteInsightGenerationJobAsync(CancellationToken.None));
        await RunAndMeasure("ForecastGenerationJob", () => _jobs.ExecuteForecastGenerationJobAsync(CancellationToken.None));
        await RunAndMeasure("CustomerIntelligenceJob", () => _jobs.ExecuteCustomerIntelligenceJobAsync(CancellationToken.None));
        await RunAndMeasure("ExecutiveSnapshotJob", () => _jobs.ExecuteExecutiveSnapshotJobAsync(CancellationToken.None));
        await RunAndMeasure("ForecastAccuracyJob", () => _jobs.ExecuteForecastAccuracyJobAsync(CancellationToken.None));
        await RunAndMeasure("AlertGenerationJob", () => _jobs.ExecuteAlertGenerationJobAsync(CancellationToken.None));

        return Ok(results);
    }
}
