using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace PosErp.Api.Middlewares;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly int _maxRequestsPerMinute;
    private readonly int _loginMaxAttemptsPerMinute;
    private readonly int _loginWindowSeconds;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _redis = redis;

        // Read rate-limiting thresholds from appsettings.json (RateLimiting section), with sensible defaults.
        var section = configuration.GetSection("RateLimiting");
        _maxRequestsPerMinute = section.GetValue<int>("GeneralMaxRequestsPerMinute", 100);
        _loginMaxAttemptsPerMinute = section.GetValue<int>("LoginMaxAttemptsPerMinute", 5);
        _loginWindowSeconds = section.GetValue<int>("LoginWindowSeconds", 60);
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

                // SEC-05: Stricter rate limit on login endpoint (per-IP).
                // This check runs BEFORE the generic limiter so the tighter login limit fires first.
                // Login POSTs still also increment the generic counter below (both counters serve
                // different purposes: login limit prevents brute-force, generic limit prevents DoS).
                if (endpoint.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                    && context.Request.Method == "POST")
                {
                    var loginKey = $"rate_limit:login:{ipAddress}";
                    var loginCount = await db.StringIncrementAsync(loginKey);
                    if (loginCount == 1)
                    {
                        await db.KeyExpireAsync(loginKey, TimeSpan.FromSeconds(_loginWindowSeconds));
                    }

                    if (loginCount > _loginMaxAttemptsPerMinute)
                    {
                        _logger.LogWarning("Login rate limit exceeded for IP: {IpAddress} ({Count} attempts in {Window}s window)",
                            ipAddress, loginCount, _loginWindowSeconds);

                        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                        context.Response.ContentType = "application/json";

                        // Match the error envelope used by GlobalExceptionMiddleware:
                        // { "statusCode": 429, "message": "..." }
                        var response = new
                        {
                            StatusCode = (int)HttpStatusCode.TooManyRequests,
                            Message = $"Too many login attempts. Please wait {_loginWindowSeconds} seconds before trying again."
                        };
                        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
                        return;
                    }
                }

                // Generic per-IP rate limit for all /api/* endpoints (including login — see comment above).
                var key = $"rate_limit:{ipAddress}";

                var count = await db.StringIncrementAsync(key);
                if (count == 1)
                {
                    await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
                }

                if (count > _maxRequestsPerMinute)
                {
                    _logger.LogWarning("Rate limit exceeded for IP: {IpAddress}", ipAddress);

                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.ContentType = "application/json";

                    // Match the error envelope used by GlobalExceptionMiddleware
                    var response = new
                    {
                        StatusCode = (int)HttpStatusCode.TooManyRequests,
                        Message = "Too many requests. Please try again later."
                    };
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Redis connection failed during rate limiting check. Proceeding without rate limiting. Error: {ErrorMessage}", ex.Message);
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
