using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services;
using QsMessaging.Shared.Services.Interfaces;
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

        private sealed class CapturingHandler : IQsMessageHandler<TestMessage>
        {
            public TestMessage? ReceivedMessage { get; private set; }

            public Task Consumer(TestMessage contractModel)
            {
                ReceivedMessage = contractModel;
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
                CreateContext(),
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
                CreateContext(),
                CancellationToken.None);

            Assert.AreEqual(3, handler.Attempts);
            errorHandler.Verify(
                x => x.HandleErrorAsync(
                    It.IsAny<InvalidOperationException>(),
                    It.Is<ErrorConsumerDetail>(detail =>
                        detail.ErrorType == ErrorConsumerType.InHandlerProblem
                        && detail.QueueName == "test-queue"
                        && detail.MessageObject is TestMessage
                        && detail.FailedMessage != null
                        && detail.FailedMessage.HandlerAttempts == 3
                        && detail.FailedMessage.Errors.Count == 3)),
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
                CreateContext(),
                CancellationToken.None);

            Assert.AreEqual(0, handler.Attempts);
            errorHandler.Verify(
                x => x.HandleErrorAsync(
                    It.IsAny<JsonException>(),
                    It.Is<ErrorConsumerDetail>(detail =>
                        detail.ErrorType == ErrorConsumerType.ReceivingProblem
                        && detail.QueueName == "test-queue"
                        && detail.MessageObject == null
                        && detail.FailedMessage != null
                        && detail.FailedMessage.HandlerAttempts == 0
                        && detail.MessageBytes != null
                        && detail.MessageBytes.SequenceEqual(invalidPayload))),
                Times.Once);
        }

        [TestMethod]
        public async Task UniversalConsumer_UsesConfiguredJsonSerializerOptions()
        {
            var handler = new CapturingHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var consumer = CreateConsumerService<CapturingHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 0,
                serialization: new QsMessagingSerializationConfiguration
                {
                    JsonSerializerOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }
                });

            await consumer.UniversalConsumer(
                Encoding.UTF8.GetBytes("""{"name":"camel"}"""),
                CreateMessageHandlerRecord<CapturingHandler>(),
                CreateContext(),
                CancellationToken.None);

            Assert.AreEqual("camel", handler.ReceivedMessage?.Name);
            errorHandler.Verify(
                x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Never);
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
                CreateContext(),
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
                CreateContext(),
                cancellationTokenSource.Token);

            errorHandler.Verify(
                x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Never);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenErrorQueueEnabledAndHandlersDisabled_SendsWrapperOnly()
        {
            var handler = new AlwaysFailHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var failedMessageQueuePublisher = new Mock<IFailedMessageQueuePublisher>();
            FailedMessageWrapper? capturedWrapper = null;
            var consumer = CreateConsumerService<AlwaysFailHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 1,
                failedMessageQueuePublisher: failedMessageQueuePublisher,
                sendToErrorQueue: true,
                callErrorHandlers: false);
            failedMessageQueuePublisher
                .Setup(publisher => publisher.SendAsync(It.IsAny<FailedMessageWrapper>(), It.IsAny<CancellationToken>()))
                .Callback<FailedMessageWrapper, CancellationToken>((wrapper, _) =>
                {
                    wrapper.SentToErrorQueueUtc = DateTimeOffset.UtcNow;
                    capturedWrapper = wrapper;
                })
                .Returns(Task.CompletedTask);

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<AlwaysFailHandler>(),
                CreateContext(),
                CancellationToken.None);

            Assert.AreEqual(2, handler.Attempts);
            Assert.IsNotNull(capturedWrapper);
            Assert.AreEqual("RabbitMQ", capturedWrapper.TransportName);
            Assert.AreEqual("test-queue", capturedWrapper.OriginalQueueName);
            Assert.AreEqual("test-queue:Error", capturedWrapper.ErrorQueueName);
            Assert.AreEqual(typeof(AlwaysFailHandler).FullName, capturedWrapper.HandlerType);
            Assert.AreEqual(2, capturedWrapper.HandlerAttempts);
            Assert.AreEqual(1, capturedWrapper.ConfiguredMaxRetryAttempts);
            Assert.AreEqual(2, capturedWrapper.Errors.Count);
            Assert.IsTrue(capturedWrapper.Errors.All(error => error.ExceptionType == typeof(InvalidOperationException).FullName));
            Assert.AreEqual("value-one", capturedWrapper.OriginalMessageHeaders["header-one"]);
            Assert.IsNotNull(capturedWrapper.SentToErrorQueueUtc);
            Assert.AreEqual(TimeSpan.Zero, capturedWrapper.CreatedUtc.Offset);
            Assert.AreEqual(TimeSpan.Zero, capturedWrapper.SentToErrorQueueUtc.Value.Offset);
            errorHandler.Verify(
                x => x.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Never);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenErrorQueueAndHandlersEnabled_RunsBothSinks()
        {
            var handler = new AlwaysFailHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var failedMessageQueuePublisher = new Mock<IFailedMessageQueuePublisher>();
            FailedMessageWrapper? queuedWrapper = null;
            ErrorConsumerDetail? errorHandlerDetail = null;
            var consumer = CreateConsumerService<AlwaysFailHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 1,
                failedMessageQueuePublisher: failedMessageQueuePublisher,
                sendToErrorQueue: true,
                callErrorHandlers: true);
            failedMessageQueuePublisher
                .Setup(publisher => publisher.SendAsync(It.IsAny<FailedMessageWrapper>(), It.IsAny<CancellationToken>()))
                .Callback<FailedMessageWrapper, CancellationToken>((wrapper, _) => queuedWrapper = wrapper)
                .Returns(Task.CompletedTask);
            errorHandler
                .Setup(handler => handler.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()))
                .Callback<Exception, ErrorConsumerDetail>((_, detail) => errorHandlerDetail = detail)
                .Returns(Task.CompletedTask);

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<AlwaysFailHandler>(),
                CreateContext(),
                CancellationToken.None);

            Assert.IsNotNull(queuedWrapper);
            Assert.IsNotNull(errorHandlerDetail?.FailedMessage);
            Assert.AreSame(queuedWrapper, errorHandlerDetail.FailedMessage);
            failedMessageQueuePublisher.Verify(
                publisher => publisher.SendAsync(It.IsAny<FailedMessageWrapper>(), It.IsAny<CancellationToken>()),
                Times.Once);
            errorHandler.Verify(
                handler => handler.HandleErrorAsync(It.IsAny<InvalidOperationException>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Once);
        }

        [TestMethod]
        public async Task UniversalConsumer_WhenErrorQueueSinkFails_StillCallsErrorHandler()
        {
            var handler = new AlwaysFailHandler();
            var errorHandler = new Mock<IQsMessagingConsumerErrorHandler>();
            var failedMessageQueuePublisher = new Mock<IFailedMessageQueuePublisher>();
            ErrorConsumerDetail? errorHandlerDetail = null;
            var consumer = CreateConsumerService<AlwaysFailHandler>(
                handler,
                errorHandler,
                maxRetryAttempts: 0,
                failedMessageQueuePublisher: failedMessageQueuePublisher,
                sendToErrorQueue: true,
                callErrorHandlers: true);
            failedMessageQueuePublisher
                .Setup(publisher => publisher.SendAsync(It.IsAny<FailedMessageWrapper>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("queue sink failed"));
            errorHandler
                .Setup(handler => handler.HandleErrorAsync(It.IsAny<Exception>(), It.IsAny<ErrorConsumerDetail>()))
                .Callback<Exception, ErrorConsumerDetail>((_, detail) => errorHandlerDetail = detail)
                .Returns(Task.CompletedTask);

            await consumer.UniversalConsumer(
                CreatePayload(),
                CreateMessageHandlerRecord<AlwaysFailHandler>(),
                CreateContext(),
                CancellationToken.None);

            Assert.IsNotNull(errorHandlerDetail?.FailedMessage);
            Assert.AreEqual("test-queue:Error", errorHandlerDetail.FailedMessage.ErrorQueueName);
            failedMessageQueuePublisher.Verify(
                publisher => publisher.SendAsync(It.IsAny<FailedMessageWrapper>(), It.IsAny<CancellationToken>()),
                Times.Once);
            errorHandler.Verify(
                handler => handler.HandleErrorAsync(It.IsAny<InvalidOperationException>(), It.IsAny<ErrorConsumerDetail>()),
                Times.Once);
        }

        private static ConsumerService CreateConsumerService<THandler>(
            THandler handler,
            Mock<IQsMessagingConsumerErrorHandler> errorHandler,
            int maxRetryAttempts,
            QsMessagingSerializationConfiguration? serialization = null,
            Mock<IFailedMessageQueuePublisher>? failedMessageQueuePublisher = null,
            bool sendToErrorQueue = false,
            bool callErrorHandlers = true)
            where THandler : class, IQsMessageHandler<TestMessage>
        {
            var services = new ServiceCollection();
            services.AddTransient<IQsMessageHandler<TestMessage>>(_ => handler);
            services.AddTransient<IQsMessagingConsumerErrorHandler>(_ => errorHandler.Object);
            var serviceProvider = services.BuildServiceProvider();
            failedMessageQueuePublisher ??= new Mock<IFailedMessageQueuePublisher>();
            failedMessageQueuePublisher
                .Setup(publisher => publisher.GetErrorQueueName(It.IsAny<ConsumerMessageContext>()))
                .Returns<ConsumerMessageContext>(context => $"{context.OriginalQueueName}:Error");
            failedMessageQueuePublisher
                .Setup(publisher => publisher.SendAsync(It.IsAny<FailedMessageWrapper>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configuration = new Mock<IQsMessagingConfiguration>();
            configuration
                .SetupGet(x => x.HandlerResilience)
                .Returns(new QsMessageHandlerRetryConfiguration
                {
                    MaxRetryAttempts = maxRetryAttempts,
                    Delay = TimeSpan.Zero
                });
            if (serialization is not null)
            {
                configuration
                    .SetupGet(x => x.Serialization)
                    .Returns(serialization);
            }
            configuration
                .SetupGet(x => x.FailedMessageHandling)
                .Returns(new QsFailedMessageHandlingConfiguration
                {
                    SendToErrorQueue = sendToErrorQueue,
                    CallErrorHandlers = callErrorHandlers
                });

            return new ConsumerService(
                Mock.Of<ILogger<ConsumerService>>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                configuration.Object,
                failedMessageQueuePublisher.Object,
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

        private static ConsumerMessageContext CreateContext()
        {
            return new ConsumerMessageContext
            {
                TransportName = "RabbitMQ",
                OriginalQueueName = "test-queue",
                OriginalHashedQueueName = "test-queue",
                OriginalDestinationName = "test-exchange",
                OriginalHashedDestinationName = "test-exchange",
                RoutingKey = "test-routing-key",
                ReplyTo = "reply-queue",
                CorrelationId = "corr-1",
                MessageId = "message-1",
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                OriginalContractType = typeof(TestMessage).FullName,
                Headers = new Dictionary<string, string?>
                {
                    ["header-one"] = "value-one"
                },
                Metadata = new Dictionary<string, string?>
                {
                    ["DeliveryTag"] = "1"
                }
            };
        }
    }
}
