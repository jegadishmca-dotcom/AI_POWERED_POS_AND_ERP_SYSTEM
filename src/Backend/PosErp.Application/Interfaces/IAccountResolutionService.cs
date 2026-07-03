using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Interfaces;

public interface IAccountResolutionService
{
    public static readonly string[] LegacyExcludedCodes = new[] { "1000", "1100", "2000", "2100", "2200", "2201", "4000", "5000" };

    Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken);
}
