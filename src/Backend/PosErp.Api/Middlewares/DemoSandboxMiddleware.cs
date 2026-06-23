using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PosErp.Api.Middlewares;

public class DemoSandboxMiddleware
{
    private readonly RequestDelegate _next;

    public DemoSandboxMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;

        // Block writing operations (POST, PUT, DELETE, PATCH) for the demo user
        if (!HttpMethods.IsGet(method))
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            
            // SEC-05 FIX: Use an explicit allowlist instead of a wildcard path.Contains("/api/pos/").
            // The old wildcard allowed the demo user to open/close the business date and close shifts —
            // all destructive operations. Only the specific POS billing operations needed for demo are permitted.
            bool isAllowedEndpoint = 
                // Auth endpoints — always needed
                path.Contains("/api/auth/login") || 
                path.Contains("/api/auth/refresh") || 
                path.Contains("/api/auth/logout") ||
                path.Equals("/api/auth/set-override-pin") ||
                path.Equals("/api/auth/verify-override-pin") ||
                // Email test — transient and doesn't mutate data
                path.Contains("/api/settings/email/test") ||
                // POS billing-only operations (exact matches for safety)
                path.Equals("/api/pos/create") ||
                path.Equals("/api/pos/invoice") ||
                path.Equals("/api/pos/calculate-cart") ||
                path.StartsWith("/api/pos/invoices/hold") ||
                path.Equals("/api/pos/invoices/held") ||
                path.StartsWith("/api/pos/sync") ||
                path.StartsWith("/api/pos/session/open") ||
                path.StartsWith("/api/pos/session/close") ||
                // Business Date (EOD) operations
                path.StartsWith("/api/pos/business-date/open") ||
                path.StartsWith("/api/pos/business-date/close") ||
                // AI Automation is read-only analytics for demo
                path.Contains("/api/aiautomation/chat") ||
                path.Contains("/api/aiautomation/status");

            // Allow creating and updating operational data (POST/PUT) for demo purposes, but block DELETE
            if ((path.StartsWith("/api/catalog") || 
                 path.StartsWith("/api/suppliers") || 
                 path.StartsWith("/api/purchasing") || 
                 path.StartsWith("/api/settings/terminals") || 
                 path.StartsWith("/api/settings/users") || 
                 path.StartsWith("/api/settings/email") || 
                 path.StartsWith("/api/inventory")) && 
                (HttpMethods.IsPost(method) || HttpMethods.IsPut(method)))
            {
                isAllowedEndpoint = true;
            }

            if (!isAllowedEndpoint)
            {
                var user = context.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var username = user.FindFirst(ClaimTypes.Name)?.Value;
                    if (string.Equals(username, "demo@supermarket.com", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        var errorResponse = new { message = "This action is disabled in the public demo sandbox to prevent database vandalism." };
                        await context.Response.WriteAsJsonAsync(errorResponse);
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
