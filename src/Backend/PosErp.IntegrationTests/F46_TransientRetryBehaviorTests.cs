using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PosErp.Application.Behaviors;
using PosErp.Application.Interfaces;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PosErp.IntegrationTests
{
    public class F46_TransientRetryBehaviorTests
    {
        // Static counters to track attempts across transient instantiation cycles
        private static int _retryableAttempts = 0;
        private static int _nonRetryableAttempts = 0;

        public record TestRetryableCommand(int TargetFailures) : IRequest<bool>, IRetryableRequest;
        public record TestNonRetryableCommand(int TargetFailures) : IRequest<bool>;

        public class TestRetryableCommandHandler : IRequestHandler<TestRetryableCommand, bool>
        {
            public async Task<bool> Handle(TestRetryableCommand request, CancellationToken cancellationToken)
            {
                _retryableAttempts++;
                if (_retryableAttempts <= request.TargetFailures)
                {
                    throw CreatePostgresException("40P01");
                }
                return true;
            }
        }

        public class TestNonRetryableCommandHandler : IRequestHandler<TestNonRetryableCommand, bool>
        {
            public async Task<bool> Handle(TestNonRetryableCommand request, CancellationToken cancellationToken)
            {
                _nonRetryableAttempts++;
                if (_nonRetryableAttempts <= request.TargetFailures)
                {
                    throw CreatePostgresException("40P01");
                }
                return true;
            }
        }

        private static PostgresException CreatePostgresException(string sqlState)
        {
            var ctor = typeof(PostgresException).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(string) },
                null);

            if (ctor != null)
            {
                return (PostgresException)ctor.Invoke(new object[] { "Transient DB Failure", "ERROR", "ERROR", sqlState });
            }
            throw new Exception("Unable to find PostgresException constructor.");
        }

        [Fact]
        public async Task RequestWithIRetryableRequest_ShouldRetryAndSucceed_IfFailuresLessThanMax()
        {
            // Arrange
            _retryableAttempts = 0; // reset
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(F46_TransientRetryBehaviorTests).Assembly);
                cfg.AddOpenBehavior(typeof(TransientRetryBehavior<,>));
            });
            services.AddTransient<IRequestHandler<TestRetryableCommand, bool>, TestRetryableCommandHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act: fails 2 times, should succeed on 3rd attempt
            var result = await mediator.Send(new TestRetryableCommand(2));

            // Assert
            Assert.True(result);
            Assert.Equal(3, _retryableAttempts);
        }

        [Fact]
        public async Task RequestWithIRetryableRequest_ShouldThrow_IfFailuresEqualOrGreaterThanMax()
        {
            // Arrange
            _retryableAttempts = 0; // reset
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(F46_TransientRetryBehaviorTests).Assembly);
                cfg.AddOpenBehavior(typeof(TransientRetryBehavior<,>));
            });
            services.AddTransient<IRequestHandler<TestRetryableCommand, bool>, TestRetryableCommandHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            // Target is 3 failures, which exceeds our 3 attempts limit
            await Assert.ThrowsAsync<PostgresException>(() => mediator.Send(new TestRetryableCommand(3)));
            Assert.Equal(3, _retryableAttempts);
        }

        [Fact]
        public async Task RequestWithoutIRetryableRequest_ShouldNotRetry_AndFailImmediately()
        {
            // Arrange
            _nonRetryableAttempts = 0; // reset
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(F46_TransientRetryBehaviorTests).Assembly);
                cfg.AddOpenBehavior(typeof(TransientRetryBehavior<,>));
            });
            services.AddTransient<IRequestHandler<TestNonRetryableCommand, bool>, TestNonRetryableCommandHandler>();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            // The command is not retryable, so it should fail immediately on the first attempt
            await Assert.ThrowsAsync<PostgresException>(() => mediator.Send(new TestNonRetryableCommand(1)));
            Assert.Equal(1, _nonRetryableAttempts);
        }
    }
}
