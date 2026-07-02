using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Interfaces;

public interface IAccountResolutionService
{
    Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken);
}
