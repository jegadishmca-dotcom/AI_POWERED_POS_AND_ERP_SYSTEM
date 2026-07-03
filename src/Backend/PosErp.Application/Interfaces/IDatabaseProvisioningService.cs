using System;
using System.Threading.Tasks;

namespace PosErp.Application.Interfaces;

public interface IDatabaseProvisioningService
{
    /// <summary>
    /// Initial tenant provisioning only. Uses CREATE DATABASE ... TEMPLATE (requires
    /// exclusive lock on source). Safe only before the source database has any active connections.
    /// </summary>
    Task ProvisionEnvironmentPairAsync(string baseConnectionString, string tenantName, Guid tenantId);

    /// <summary>
    /// Recurring UAT refresh (Section 3.2). Uses pg_dump + pg_restore so _live stays
    /// fully online during the operation. Use this for all periodic resets once the store is trading.
    /// </summary>
    Task RefreshUatFromLiveSnapshotAsync(string liveConnectionString, string uatConnectionString, Guid tenantId);
}
