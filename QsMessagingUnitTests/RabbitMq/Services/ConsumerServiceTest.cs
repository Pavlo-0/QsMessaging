using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class ConsumerServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RqConsumerService>> _mockLogger;
        private Mock<IConsumerService> _mockInnerConsumerService;
        private Mock<IChannel> _mockChannel;
        private IRqConsumerService _consumerService;
#pragma warning restore CS8618

        private class TestModel { }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RqConsumerService>>();
            _mockInnerConsumerService = new Mock<IConsumerService>();
            _mockChannel = new Mock<IChannel>();
            _mockChannel.SetupGet(c => c.IsOpen).Returns(true);

            _consumerService = new RqConsumerService(_mockLogger.Object, _mockInnerConsumerService.Object);

            // Reset static bag between tests
            var field = typeof(RqConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<RqStoreConsumerRecord>)field!.GetValue(null)!;
            bag.Clear();
        }

        [TestMethod]
        public async Task GetOrCreateConsumerAsync_WhenNoPreviousConsumerExists_CreatesAndReturnsConsumerTag()
        {
            const string expectedTag = "test-consumer-tag";
            const string queueName = "test-queue";

            _mockChannel
                .Setup(c => c.BasicConsumeAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(),
                    It.IsAny<IAsyncBasicConsumer>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedTag);

            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(TestModel),
                typeof(TestModel));

            var result = await _consumerService.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record);

            Assert.AreEqual(expectedTag, result);
            _mockChannel.Verify(c => c.BasicConsumeAsync(
                queueName,
                false,
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateConsumerAsync_WhenConsumerAlreadyRegistered_ReturnsExistingConsumerTagWithoutCreatingNew()
        {
            const string existingTag = "existing-tag";
            const string queueName = "test-queue";

            var field = typeof(RqConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<RqStoreConsumerRecord>)field!.GetValue(null)!;
            bag.Add(new RqStoreConsumerRecord(_mockChannel.Object, queueName, existingTag));

            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(TestModel),
                typeof(TestModel));

            var result = await _consumerService.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record);

            Assert.AreEqual(existingTag, result);
            _mockChannel.Verify(c => c.BasicConsumeAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task GetOrCreateConsumerAsync_WhenMessageIsReceived_AwaitsInnerConsumerService()
        {
            const string consumerTag = "test-consumer-tag";
            const string queueName = "test-queue";
            const string correlationId = "corr-1";
            const string replyTo = "reply-queue";
            var expectedPayload = JsonSerializer.Serialize(new TestModel());
            IAsyncBasicConsumer? registeredConsumer = null;
            var processingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowProcessingToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _mockChannel
                .Setup(c => c.BasicConsumeAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(),
                    It.IsAny<IAsyncBasicConsumer>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, bool, string, bool, bool, IDictionary<string, object?>?, IAsyncBasicConsumer, CancellationToken>((_, _, _, _, _, _, consumer, _) => registeredConsumer = consumer)
                .ReturnsAsync(consumerTag);

            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(TestModel));

            _mockInnerConsumerService
                .Setup(s => s.UniversalConsumer(
                    It.IsAny<byte[]>(),
                    record,
                    correlationId,
                    replyTo,
                    queueName,
                    CancellationToken.None))
                .Returns(async () =>
                {
                    processingStarted.TrySetResult();
                    await allowProcessingToFinish.Task;
                });
            _mockChannel
                .Setup(c => c.BasicAckAsync(1UL, false, It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            await _consumerService.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record);

            Assert.IsNotNull(registeredConsumer);

            var deliveryTask = RaiseReceivedAsync(
                registeredConsumer!,
                Encoding.UTF8.GetBytes(expectedPayload),
                correlationId,
                replyTo);

            await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.IsFalse(deliveryTask.IsCompleted);
            _mockChannel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

            allowProcessingToFinish.TrySetResult();

            await deliveryTask.WaitAsync(TimeSpan.FromSeconds(2));
            _mockInnerConsumerService.Verify(s => s.UniversalConsumer(
                It.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == expectedPayload),
                record,
                correlationId,
                replyTo,
                queueName,
                CancellationToken.None), Times.Once);
            _mockChannel.Verify(c => c.BasicAckAsync(1UL, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockChannel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task GetOrCreateConsumerAsync_WhenMessageProcessingThrows_AcksAndDoesNotNack()
        {
            const string consumerTag = "test-consumer-tag";
            const string queueName = "test-queue";
            IAsyncBasicConsumer? registeredConsumer = null;

            _mockChannel
                .Setup(c => c.BasicConsumeAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<IDictionary<string, object?>?>(),
                    It.IsAny<IAsyncBasicConsumer>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, bool, string, bool, bool, IDictionary<string, object?>?, IAsyncBasicConsumer, CancellationToken>((_, _, _, _, _, _, consumer, _) => registeredConsumer = consumer)
                .ReturnsAsync(consumerTag);

            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(TestModel));

            _mockInnerConsumerService
                .Setup(s => s.UniversalConsumer(
                    It.IsAny<byte[]>(),
                    record,
                    It.IsAny<string?>(),
                    It.IsAny<string>(),
                    queueName,
                    CancellationToken.None))
                .ThrowsAsync(new InvalidOperationException("boom"));
            _mockChannel
                .Setup(c => c.BasicAckAsync(1UL, false, It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            await _consumerService.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record);

            Assert.IsNotNull(registeredConsumer);

            await RaiseReceivedAsync(
                registeredConsumer!,
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestModel())));

            _mockChannel.Verify(c => c.BasicAckAsync(1UL, false, It.IsAny<CancellationToken>()), Times.Once);
            _mockChannel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public void GetConsumersByChannel_WhenNoConsumersExist_ReturnsEmpty()
        {
            var result = _consumerService.GetConsumersByChannel(_mockChannel.Object);

            Assert.IsFalse(result.Any());
        }

        [TestMethod]
        public void GetConsumersByChannel_WhenConsumersExistForChannel_ReturnsMatchingTags()
        {
            const string consumerTag = "tag1";

            var field = typeof(RqConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<RqStoreConsumerRecord>)field!.GetValue(null)!;
            bag.Add(new RqStoreConsumerRecord(_mockChannel.Object, "queue1", consumerTag));

            var result = _consumerService.GetConsumersByChannel(_mockChannel.Object).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(consumerTag, result[0]);
        }

        [TestMethod]
        public void GetConsumersByChannel_WhenConsumersExistForDifferentChannel_ReturnsEmpty()
        {
            var otherChannel = new Mock<IChannel>();

            var field = typeof(RqConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<RqStoreConsumerRecord>)field!.GetValue(null)!;
            bag.Add(new RqStoreConsumerRecord(otherChannel.Object, "queue1", "tag1"));

            var result = _consumerService.GetConsumersByChannel(_mockChannel.Object);

            Assert.IsFalse(result.Any());
        }

        private static Task RaiseReceivedAsync(IAsyncBasicConsumer consumer, byte[] body, string correlationId = "", string replyTo = "")
        {
            var handleBasicDeliverAsync = consumer.GetType().GetMethod("HandleBasicDeliverAsync")
                ?? throw new InvalidOperationException("RabbitMQ consumer does not expose HandleBasicDeliverAsync.");

            var arguments = handleBasicDeliverAsync
                .GetParameters()
                .Select(parameter => CreateHandleBasicDeliverArgument(parameter, body, correlationId, replyTo))
                .ToArray();

            return (Task)(handleBasicDeliverAsync.Invoke(consumer, arguments) ?? Task.CompletedTask);
        }

        private static object? CreateHandleBasicDeliverArgument(ParameterInfo parameter, byte[] body, string correlationId, string replyTo)
        {
            if (parameter.ParameterType == typeof(string))
            {
                return parameter.Name switch
                {
                    "consumerTag" => "test-tag",
                    "exchange" => string.Empty,
                    "routingKey" => string.Empty,
                    _ => correlationId,
                };
            }

            if (parameter.ParameterType == typeof(ulong))
            {
                return 1UL;
            }

            if (parameter.ParameterType == typeof(bool))
            {
                return false;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                return CancellationToken.None;
            }

            if (parameter.ParameterType == typeof(ReadOnlyMemory<byte>))
            {
                return new ReadOnlyMemory<byte>(body);
            }

            if (parameter.ParameterType == typeof(byte[]))
            {
                return body;
            }

            if (typeof(IReadOnlyBasicProperties).IsAssignableFrom(parameter.ParameterType)
                || typeof(IBasicProperties).IsAssignableFrom(parameter.ParameterType))
            {
                return new BasicProperties
                {
                    CorrelationId = correlationId,
                    ReplyTo = replyTo,
                };
            }

            throw new NotSupportedException($"Unsupported HandleBasicDeliverAsync parameter: {parameter.ParameterType.FullName}");
        }
    }
}
