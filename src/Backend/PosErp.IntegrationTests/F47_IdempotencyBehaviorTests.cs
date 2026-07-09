using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PosErp.Application.Behaviors;
using PosErp.Application.Exceptions;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Common;
using PosErp.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PosErp.IntegrationTests
{
    public class F47_IdempotencyBehaviorTests
    {
        private static int _handlerExecutions = 0;

        public record TestIdempotentCommand(Guid? ClientRequestToken, bool ShouldFail) 
            : IRequest<string>, IIdempotentRequest;

        public class TestIdempotentCommandHandler : IRequestHandler<TestIdempotentCommand, string>
        {
            public async Task<string> Handle(TestIdempotentCommand request, CancellationToken cancellationToken)
            {
                _handlerExecutions++;
                if (request.ShouldFail)
                {
                    throw new InvalidOperationException("Simulated Handler Failure");
                }
                return $"Success-Result-{_handlerExecutions}";
            }
        }

        private ServiceProvider BuildServiceProvider(ApplicationDbContext dbContext)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IApplicationDbContext>(dbContext);
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(F47_IdempotencyBehaviorTests).Assembly);
                cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            });
            services.AddTransient<IRequestHandler<TestIdempotentCommand, string>, TestIdempotentCommandHandler>();
            return services.BuildServiceProvider();
        }

        private ApplicationDbContext CreateTestDbContext()
        {
            // Connect to integration test database without dropping/rebuilding schema
            var db = IntegrationTestDbFactory.CreateNewContext();
            
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS idempotent_requests (
                    client_request_token UUID PRIMARY KEY,
                    status TEXT NOT NULL,
                    response_payload TEXT,
                    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
                    tenant_id UUID NOT NULL
                );");

            // Clean up any stale test records from previous test runs
            db.Database.ExecuteSqlRaw("DELETE FROM idempotent_requests;");
            return db;
        }

        [Fact]
        public async Task UniqueToken_ShouldExecuteHandlerOnce()
        {
            // Arrange
            _handlerExecutions = 0;
            using var db = CreateTestDbContext();
            var provider = BuildServiceProvider(db);
            var mediator = provider.GetRequiredService<IMediator>();
            var token = Guid.NewGuid();

            // Act
            var res1 = await mediator.Send(new TestIdempotentCommand(token, false));

            // Assert
            Assert.Equal("Success-Result-1", res1);
            Assert.Equal(1, _handlerExecutions);

            // Query DB to verify completed state
            var record = await db.IdempotentRequests.FindAsync(token);
            Assert.NotNull(record);
            Assert.Equal("COMPLETED", record.Status);
            Assert.Contains("Success-Result-1", record.ResponsePayload ?? "");
        }

        [Fact]
        public async Task ReusingCompletedToken_ShouldReturnCachedResult_WithoutExecutingHandlerAgain()
        {
            // Arrange
            _handlerExecutions = 0;
            using var db = CreateTestDbContext();
            var provider = BuildServiceProvider(db);
            var mediator = provider.GetRequiredService<IMediator>();
            var token = Guid.NewGuid();

            // Act
            var res1 = await mediator.Send(new TestIdempotentCommand(token, false));
            var res2 = await mediator.Send(new TestIdempotentCommand(token, false));

            // Assert
            Assert.Equal("Success-Result-1", res1);
            Assert.Equal("Success-Result-1", res2); // cached result returned
            Assert.Equal(1, _handlerExecutions); // Handler only executed once!
        }

        [Fact]
        public async Task ConcurrentDuplicateToken_ShouldThrowConflictException()
        {
            // Arrange
            _handlerExecutions = 0;
            using var db = CreateTestDbContext();
            var provider = BuildServiceProvider(db);
            var mediator = provider.GetRequiredService<IMediator>();
            var token = Guid.NewGuid();

            // Pre-seed a PENDING row in DB to simulate a concurrent request currently executing
            db.IdempotentRequests.Add(new IdempotentRequest
            {
                ClientRequestToken = token,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ConflictException>(() => 
                mediator.Send(new TestIdempotentCommand(token, false)));
            Assert.Equal(0, _handlerExecutions); // Handler never runs!
        }

        [Fact]
        public async Task StalePendingToken_ShouldAllowRetry()
        {
            // Arrange
            _handlerExecutions = 0;
            using var db = CreateTestDbContext();
            var provider = BuildServiceProvider(db);
            var mediator = provider.GetRequiredService<IMediator>();
            var token = Guid.NewGuid();

            // Pre-seed a stale PENDING row (created 20 mins ago)
            db.IdempotentRequests.Add(new IdempotentRequest
            {
                ClientRequestToken = token,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            });
            await db.SaveChangesAsync();

            // Act
            var result = await mediator.Send(new TestIdempotentCommand(token, false));

            // Assert
            Assert.Equal("Success-Result-1", result);
            Assert.Equal(1, _handlerExecutions);
        }

        [Fact]
        public async Task FailedRequest_ShouldTransitionToFailedState_AndAllowRetry()
        {
            // Arrange
            _handlerExecutions = 0;
            using var db = CreateTestDbContext();
            var provider = BuildServiceProvider(db);
            var mediator = provider.GetRequiredService<IMediator>();
            var token = Guid.NewGuid();

            // Act 1: Send request that fails
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                mediator.Send(new TestIdempotentCommand(token, true)));
            
            Assert.Equal(1, _handlerExecutions);
            var record = await db.IdempotentRequests.FindAsync(token);
            Assert.NotNull(record);
            Assert.Equal("FAILED", record.Status);

            // Act 2: Retry request (success path this time)
            var result = await mediator.Send(new TestIdempotentCommand(token, false));

            // Assert
            Assert.Equal("Success-Result-2", result);
            Assert.Equal(2, _handlerExecutions);
            
            var recordAfterSuccess = await db.IdempotentRequests.FindAsync(token);
            Assert.NotNull(recordAfterSuccess);
            Assert.Equal("COMPLETED", recordAfterSuccess.Status);
        }

        [Fact]
        public async Task RetryImmediatelyAfterStalePendingRecovery_ShouldSucceed()
        {
            // Arrange
            _handlerExecutions = 0;
            using var db = CreateTestDbContext();
            var provider = BuildServiceProvider(db);
            var mediator = provider.GetRequiredService<IMediator>();
            var token = Guid.NewGuid();

            // Pre-seed a stale PENDING row (created 20 mins ago)
            db.IdempotentRequests.Add(new IdempotentRequest
            {
                ClientRequestToken = token,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            });
            await db.SaveChangesAsync();

            // Act 1: Send request that immediately triggers stale recovery and succeeds
            var result1 = await mediator.Send(new TestIdempotentCommand(token, false));

            // Act 2: Send request again immediately (which should get the completed cached result)
            var result2 = await mediator.Send(new TestIdempotentCommand(token, false));

            // Assert
            Assert.Equal("Success-Result-1", result1);
            Assert.Equal("Success-Result-1", result2);
            Assert.Equal(1, _handlerExecutions); // handler ran only once after recovery!
        }

        [Fact]
        public async Task DuplicateTokenAcrossDifferentTenants_ShouldBeBlockedByGlobalPK()
        {
            // Arrange
            using var dbA = CreateTestDbContext();
            var token = Guid.NewGuid();

            // Seed token for tenant A
            dbA.IdempotentRequests.Add(new IdempotentRequest
            {
                ClientRequestToken = token,
                Status = "COMPLETED",
                TenantId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            });
            await dbA.SaveChangesAsync();

            // Act & Assert
            // Use a separate database context to simulate a duplicate token insertion attempt from another tenant
            using var dbB = IntegrationTestDbFactory.CreateNewContext();
            var duplicateRequest = new IdempotentRequest
            {
                ClientRequestToken = token,
                Status = "PENDING",
                TenantId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            dbB.IdempotentRequests.Add(duplicateRequest);

            // SaveChanges should fail with a database unique key violation exception
            await Assert.ThrowsAnyAsync<Exception>(() => dbB.SaveChangesAsync());
        }

        [Fact]
        public async Task ExpiredIdempotencyRecords_ShouldBeCleanedUpByQuery()
        {
            // Arrange
            using var db = CreateTestDbContext();
            var tokenExpired = Guid.NewGuid();
            var tokenFresh = Guid.NewGuid();

            // Seed expired request (25 hours ago)
            db.IdempotentRequests.Add(new IdempotentRequest
            {
                ClientRequestToken = tokenExpired,
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow.AddHours(-25)
            });

            // Seed fresh request (2 hours ago)
            db.IdempotentRequests.Add(new IdempotentRequest
            {
                ClientRequestToken = tokenFresh,
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            });
            await db.SaveChangesAsync();

            // Act: run cleanup query (simulating the background task behavior)
            var cutoff = DateTime.UtcNow.AddHours(-24);
            int deleted = await db.IdempotentRequests
                .Where(r => r.CreatedAt < cutoff)
                .ExecuteDeleteAsync();

            // Assert
            Assert.Equal(1, deleted);

            // Clear the local DbContext tracking cache so subsequent FindAsync calls run direct queries against the database
            db.ChangeTracker.Clear();

            var expiredRecord = await db.IdempotentRequests.FindAsync(tokenExpired);
            var freshRecord = await db.IdempotentRequests.FindAsync(tokenFresh);
            Assert.Null(expiredRecord);
            Assert.NotNull(freshRecord);
        }
    }
}
