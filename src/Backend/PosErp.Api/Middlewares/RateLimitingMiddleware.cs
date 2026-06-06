using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Net;
using System.Threading.Tasks;

namespace PosErp.Api.Middlewares;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConnectionMultiplexer _redis;
    private const int MaxRequestsPerMinute = 100;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConnectionMultiplexer redis)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // SEC-02 FIX: Prefer X-Forwarded-For when running behind a reverse proxy (Nginx/Render).
        // Without this, all requests appear to come from the proxy IP, sharing one rate limit bucket.
        // We validate the header is a real IP and fall back to the direct connection address.
        string ipAddress = GetClientIpAddress(context);
        var endpoint = context.Request.Path.Value;

        // Only rate limit API calls
        if (endpoint != null && endpoint.StartsWith("/api"))
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"rate_limit:{ipAddress}";

                var count = await db.StringIncrementAsync(key);
                if (count == 1)
                {
                    await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
                }

                if (count > MaxRequestsPerMinute)
                {
                    _logger.LogWarning($"Rate limit exceeded for IP: {ipAddress}");
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\": \"Too many requests. Please try again later.\"}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Redis connection failed during rate limiting check. Proceeding without rate limiting. Error: {ex.Message}");
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Returns the real client IP address. Reads X-Forwarded-For first (for proxy deployments),
    /// validates it is a valid IP address to prevent header injection, and falls back to the
    /// direct TCP connection remote address.
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        // X-Forwarded-For may contain a comma-separated list: "client, proxy1, proxy2"
        // The leftmost value is the original client IP.
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(firstIp, out _))
            {
                return firstIp;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
