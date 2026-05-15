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
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpoint = context.Request.Path.Value;

        // Only rate limit API calls
        if (endpoint != null && endpoint.StartsWith("/api"))
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

        await _next(context);
    }
}
