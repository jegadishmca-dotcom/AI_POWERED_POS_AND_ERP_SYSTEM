using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosErp.Application.Exceptions;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Common;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Behaviors
{
    public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> 
        where TRequest : IRequest<TResponse>, IIdempotentRequest
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>>? _logger;

        public IdempotencyBehavior(IApplicationDbContext context, ILogger<IdempotencyBehavior<TRequest, TResponse>>? logger = null)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!request.ClientRequestToken.HasValue || request.ClientRequestToken.Value == Guid.Empty)
            {
                return await next();
            }

            var token = request.ClientRequestToken.Value;

            // Query existing request token details
            var existing = await _context.IdempotentRequests
                .FirstOrDefaultAsync(r => r.ClientRequestToken == token, cancellationToken);

            if (existing != null)
            {
                if (existing.Status == "PENDING")
                {
                    // Check if stale (older than 15 mins - e.g., application crashed during checkout)
                    if (DateTime.UtcNow - existing.CreatedAt > TimeSpan.FromMinutes(15))
                    {
                        _logger?.LogWarning("Stale PENDING idempotency request detected for token {Token}. Allowing retry.", token);
                        existing.Status = "PENDING";
                        existing.CreatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        throw new ConflictException("A concurrent transaction is already processing this request token.");
                    }
                }
                else if (existing.Status == "COMPLETED")
                {
                    _logger?.LogInformation("Idempotency match found for token {Token}. Returning cached response payload.", token);
                    if (!string.IsNullOrEmpty(existing.ResponsePayload))
                    {
                        var cachedResponse = JsonSerializer.Deserialize<TResponse>(existing.ResponsePayload);
                        if (cachedResponse != null)
                        {
                            return cachedResponse;
                        }
                    }
                    throw new InvalidOperationException("Idempotency record status is COMPLETED but response payload is empty.");
                }
                else if (existing.Status == "FAILED")
                {
                    // Transition back to PENDING to retry execution
                    existing.Status = "PENDING";
                    existing.CreatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            else
            {
                // Create a new PENDING request tracking row
                var newReq = new IdempotentRequest
                {
                    ClientRequestToken = token,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };
                _context.IdempotentRequests.Add(newReq);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Concurrency race fallback: if another thread inserted it first
                    ((DbContext)_context).Entry(newReq).State = EntityState.Detached;
                    _logger?.LogWarning(ex, "Idempotency race detected. Fetching existing insertion for token {Token}.", token);
                    
                    var concurrentReq = await _context.IdempotentRequests
                        .FirstOrDefaultAsync(r => r.ClientRequestToken == token, cancellationToken);

                    if (concurrentReq != null)
                    {
                        if (concurrentReq.Status == "PENDING" && DateTime.UtcNow - concurrentReq.CreatedAt <= TimeSpan.FromMinutes(15))
                        {
                            throw new ConflictException("A concurrent transaction is already processing this request token.");
                        }
                        if (concurrentReq.Status == "COMPLETED" && !string.IsNullOrEmpty(concurrentReq.ResponsePayload))
                        {
                            var cachedResponse = JsonSerializer.Deserialize<TResponse>(concurrentReq.ResponsePayload);
                            if (cachedResponse != null) return cachedResponse;
                        }
                    }
                    throw;
                }
            }

            TResponse response;
            try
            {
                response = await next();
            }
            catch (Exception)
            {
                // On execution failure, reset state to FAILED to allow clean subsequent retries
                var reqToFail = await _context.IdempotentRequests
                    .FirstOrDefaultAsync(r => r.ClientRequestToken == token, cancellationToken);
                if (reqToFail != null)
                {
                    reqToFail.Status = "FAILED";
                    await _context.SaveChangesAsync(cancellationToken);
                }
                throw;
            }

            // On execution success, persist cached response and update state to COMPLETED
            var reqToComplete = await _context.IdempotentRequests
                .FirstOrDefaultAsync(r => r.ClientRequestToken == token, cancellationToken);
            if (reqToComplete != null)
            {
                reqToComplete.Status = "COMPLETED";
                reqToComplete.ResponsePayload = JsonSerializer.Serialize(response);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return response;
        }
    }
}
