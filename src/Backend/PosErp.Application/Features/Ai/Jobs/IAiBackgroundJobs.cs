using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Ai.Jobs;

public interface IAiBackgroundJobs
{
    Task ExecuteInsightGenerationJobAsync(CancellationToken cancellationToken);
    Task ExecuteForecastGenerationJobAsync(CancellationToken cancellationToken);
    Task ExecuteCustomerIntelligenceJobAsync(CancellationToken cancellationToken);
    Task ExecuteExecutiveSnapshotJobAsync(CancellationToken cancellationToken);
    Task ExecuteForecastAccuracyJobAsync(CancellationToken cancellationToken);
    Task ExecuteAlertGenerationJobAsync(CancellationToken cancellationToken);
}
