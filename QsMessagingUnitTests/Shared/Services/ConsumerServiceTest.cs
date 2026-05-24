using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services;
using System.Text;
using System.Text.Json;

namespace QsMessagingUnitTests.Shared.Services
{
    [TestClass]
    public class ConsumerServiceTest
    {
        private sealed class TestMessage
        {
            public string Name { get; set; } = string.Empty;
        }

        private sealed class RetryThenSucceedHandler : IQsMessageHandler<TestMessage>
        {
            public int Attempts { get; private set; }

            public async Task Consumer(TestMessage contractModel)
            {
                Attempts++;
                if (Attempts == 1)
                {
                    await Task.Yield();
                    throw new InvalidOperationException("transient handler failure");
                }
            }
        }

        private sealed class AlwaysFailHandler : IQsMessageHandler<TestMessage>
        {
            public int Attempts { get; private set; }

            public async Task Consumer(TestMessage contractModel)
            {
                Attempts++;
                await Task.Yield();
                throw new InvalidOperationException("permanent handler failure");
            }
        }

        private sealed class CancellableHandler : IQsMessageHandler<TestMessage>
        {
            public CancellationToken ReceivedCancellationToken { get; private set; }

            public Task Consumer(TestMessage contractModel)
            {
                throw new InvalidOperationException("The non-cancellable overload should not be used when a cancellable overload exists.");
            }

            public Task Consumer(TestMessage contractModel, CancellationToken cancellationToken)
            {
                ReceivedCancellationToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        private sealed class CancelledHandler : IQsMessageHandler<TestMessage>
        {
            public Task Consumer(TestMessage contractModel)
            {
                throw new InvalidOperationException("The non-cancellable overload should not be used when a cancellable overload exists.");
            }

            public Task Consumer(TestMessage contractModel, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenHandlerRetrySucceeds_DoesNotCallErrorHandler()
        {
            var handler = new RetryThenSucceedHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var consumer = CreateConsumerService<RetryThenSucceedHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 1);

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<RetryThenSucceedHandler>(),
                null,
                string.Empty,
                "test-queue",
                CancellationToken.None);

            Assert.AreEqual(2, handler.Attempts);
            errorHandler.Verify(
                x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Never);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenHandlerStillFailsAfterRetries_CallsErrorHandlerOnce()
        {
            var handler = new AlwaysFailHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var consumer = CreateConsumerService<AlwaysFailHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 2);

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<AlwaysFailHandler>(),
                null,
                string.Empty,
                "test-queue",
                CancellationToken.None);

            Assert.AreEqual(3, handler.Attempts);
            errorHandler.Verify(
                x => x.HandleErrorAsync(
                    It.IsAny<InvalidOperationException>(),
                    It.Is<ErrorConsumerDetail>(detail =>
                        detail.ErrorType == ErrorConsumerType.InHandlerProblem
                        && detail.QueueName == "test-queue"
                        && detail.MessageObject is TestMessage)),
                Times.Once);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenPayloadCannotDeserialize_CallsErrorHandlerWithReceivingProblem()
        {
            var invalidPayload = Encoding.UTF8.GetBytes("{");
            var handler = new RetryThenSucceedHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var consumer = CreateConsumerService<RetryThenSucceedHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 0);

            await consumer.UniversalConsumer(
                invalidPayload,
                CreateMessageHandlerRecord<RetryThenSucceedHandler>(),
                null,
                string.Empty,
                "test-queue",
                CancellationToken.None);

            Assert.AreEqual(0, handler.Attempts);
            errorHandler.Verify(
                x => x.HandleErrorAsync(
                    It.IsAny<JsonException>(),
                    It.Is<ErrorConsumerDetail>(detail =>
                        detail.ErrorType == ErrorConsumerType.ReceivingProblem
                        && detail.QueueName == "test-queue"
                        && detail.MessageObject == null
                        && detail.MessageBytes != null
                        && detail.MessageBytes.SequenceEqual(invalidPayload))),
                Times.Once);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenHandlerHasCancellationTokenOverload_PassesToken()
        {
            var handler = new CancellableHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var consumer = CreateConsumerService<CancellableHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 0);
            using var cancellationTokenSource = new CancellationTokenSource();

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<CancellableHandler>(),
                null,
                string.Empty,
                "test-queue",
                cancellationTokenSource.Token);

            Assert.AreEqual(cancellationTokenSource.Token, handler.ReceivedCancellationToken);
            errorHandler.Verify(
                x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Never);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenCancellationTokenIsCancelled_DoesNotCallErrorHandler()
        {
            var handler = new CancelledHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var consumer = CreateConsumerService<CancelledHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 0);
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<CancelledHandler>(),
                null,
                string.Empty,
                "test-queue",
                cancellationTokenSource.Token);

            errorHandler.Verify(
                x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Never);
        }

        private static ConsumerService CreateConsumerService<THandler>(
            THandler handler,
            Mock<IQsMessagingConsumerErrorHandler> errorHandler,
            int maxRetryAttempts)
            where THandler : class, IQsMessageHandler<TestMessage>
        {
            var services = new ServiceCollection();
            services.AddTransient<IQsMessageHandler<TestMessage>>(_ => handler);
            services.AddTransient<IQsMessagingConsumerErrorHandler>(_ => errorHandler.Object);
            var serviceProvider = services.BuildServiceProvider();

            var configuration = new Mock<IQsMessagingConfiguration>();
            configuration
                .SetupGet(x => x.HandlerResilience)
                .Returns(new QsMessageHandlerRetryConfiguration
                {
                    MaxRetryAttempts = maxRetryAttempts,
                    Delay = TimeSpan.Zero
                });

            return new ConsumerService(
                Mock.Of<ILogger<ConsumerService>>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                configuration.Object,
                Mock.Of<ISender>());
        }

        private static HandlersStoreRecord CreateMessageHandlerRecord<THandler>()
            where THandler : IQsMessageHandler<TestMessage>
        {
            return new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestMessage>),
                typeof(THandler),
                typeof(TestMessage));
        }

        private static byte[] CreatePayload()
        {
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestMessage { Name = "test" }));
        }
    }
}
