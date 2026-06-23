using Hangfire.Dashboard;

namespace PosErp.Api.Infrastructure;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // For local development, testing, and sandbox environments, allow all dashboard requests.
        return true;
    }
}
