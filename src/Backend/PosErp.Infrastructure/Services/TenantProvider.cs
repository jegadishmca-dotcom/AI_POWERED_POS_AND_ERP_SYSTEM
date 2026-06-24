using Microsoft.AspNetCore.Http;
using PosErp.Application.Interfaces;
using System;
using System.Linq;

namespace PosErp.Infrastructure.Services;

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid _tenantId;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            if (_tenantId != Guid.Empty)
            {
                return _tenantId;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                // Return default/empty if not in a web request context (e.g., background job)
                return Guid.Empty;
            }

            var tenantClaim = httpContext.User.Claims.FirstOrDefault(c => c.Type == "TenantId");
            if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
            {
                _tenantId = tenantId;
                return _tenantId;
            }

            // Fallback header
            if (httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenantId))
            {
                if (Guid.TryParse(headerTenantId.FirstOrDefault(), out var parsedTenantId))
                {
                    _tenantId = parsedTenantId;
                    return _tenantId;
                }
            }

            return Guid.Empty;
        }
    }

    public void SetTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}
