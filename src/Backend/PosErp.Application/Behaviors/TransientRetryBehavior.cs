using MediatR;
using Microsoft.Extensions.Logging;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Behaviors
{
    public class TransientRetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> 
        where TRequest : IRequest<TResponse>, IRetryableRequest
    {
        private readonly ILogger<TransientRetryBehavior<TRequest, TResponse>>? _logger;

        public TransientRetryBehavior(ILogger<TransientRetryBehavior<TRequest, TResponse>>? logger = null)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            const int baseDelayMs = 100;
            var random = new Random();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var start = DateTime.UtcNow;
                try
                {
                    var result = await next();
                    if (attempt > 1)
                    {
                        var duration = (DateTime.UtcNow - start).TotalMilliseconds;
                        _logger?.LogInformation(
                            "TransientRetryBehavior: Request of type {RequestType} succeeded on attempt #{Attempt}. Executed in {Duration}ms.",
                            typeof(TRequest).Name, attempt, duration);
                    }
                    return result;
                }
                catch (Exception ex) when (IsTransientPostgresError(ex))
                {
                    var pgCode = GetPostgresErrorCode(ex) ?? "Unknown";

                    if (attempt == maxAttempts)
                    {
                        _logger?.LogError(
                            ex,
                            "TransientRetryBehavior: Request of type {RequestType} failed after maximum retry attempts ({MaxAttempts}). Final error code: {ErrorCode}.",
                            typeof(TRequest).Name, maxAttempts, pgCode);
                        throw;
                    }

                    // Exponential backoff with jitter to spread retries
                    int backoffMs = (int)(baseDelayMs * Math.Pow(2, attempt - 1)) + random.Next(10, 50);

                    _logger?.LogWarning(
                        ex,
                        "TransientRetryBehavior: Request of type {RequestType} failed with transient error code '{ErrorCode}' (attempt #{Attempt}/{MaxAttempts}). Retrying in {BackoffMs}ms...",
                        typeof(TRequest).Name, pgCode, attempt, maxAttempts, backoffMs);

                    await Task.Delay(backoffMs, cancellationToken);
                }
            }

            throw new InvalidOperationException("Unreachable retry pipeline end state.");
        }

        private static bool IsTransientPostgresError(Exception ex)
        {
            var pgCode = GetPostgresErrorCode(ex);
            return pgCode == "40P01" || pgCode == "55P03" || pgCode == "40001";
        }

        private static string? GetPostgresErrorCode(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is Npgsql.PostgresException pgEx)
                {
                    return pgEx.SqlState;
                }
                current = current.InnerException;
            }
            return null;
        }
    }
}
