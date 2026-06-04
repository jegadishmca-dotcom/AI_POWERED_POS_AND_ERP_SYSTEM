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

        // Block writing operations (POST, PUT, DELETE, PATCH)
        if (!HttpMethods.IsGet(method))
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            
            // Exception: Allow logins, logouts, token refresh, and the transient SMTP test
            bool isAllowedEndpoint = path.Contains("/api/auth/login") || 
                                     path.Contains("/api/auth/refresh") || 
                                     path.Contains("/api/auth/logout") ||
                                     path.Contains("/api/settings/email/test") ||
                                     path.Contains("/api/pos/") ||
                                     path.Contains("/api/aiautomation/");

            // Allow creating and updating products (POST/PUT) for demo purposes, but block DELETE
            if (path.StartsWith("/api/catalog") && (HttpMethods.IsPost(method) || HttpMethods.IsPut(method)))
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
