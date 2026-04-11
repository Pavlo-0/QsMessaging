using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared.Interface;
using RabbitMQ.Client;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class SubscriberTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RqSubscriber>> _mockLogger;
        private Mock<IConnectionService> _mockConnectionService;
        private Mock<IChannelService> _mockChannelService;
        private Mock<IExchangeService> _mockExchangeService;
        private Mock<IQueueService> _mockQueueService;
        private Mock<IHandlerService> _mockHandlerService;
        private Mock<IConsumerService> _mockConsumerService;
        private Mock<IConnection> _mockConnection;
        private Mock<IChannel> _mockChannel;
        private ISubscriber _subscriber;
#pragma warning restore CS8618

        private class TestModel { }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RqSubscriber>>();
            _mockConnectionService = new Mock<IConnectionService>();
            _mockChannelService = new Mock<IChannelService>();
            _mockExchangeService = new Mock<IExchangeService>();
            _mockQueueService = new Mock<IQueueService>();
            _mockHandlerService = new Mock<IHandlerService>();
            _mockConsumerService = new Mock<IConsumerService>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IChannel>();

            _subscriber = new RqSubscriber(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockChannelService.Object,
                _mockExchangeService.Object,
                _mockQueueService.Object,
                _mockHandlerService.Object,
                _mockConsumerService.Object);
        }

        [TestMethod]
        public async Task SubscribeAsync_WhenNoHandlers_DoesNotCallConnectionService()
        {
            _mockHandlerService.Setup(h => h.GetHandlers()).Returns(Enumerable.Empty<HandlersStoreRecord>());

            await _subscriber.SubscribeAsync();

            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task SubscribeAsync_WhenTwoHandlersExist_CreatesConsumerForEach()
        {
            var record1 = new HandlersStoreRecord(typeof(IQsMessageHandler<>), typeof(IQsMessageHandler<TestModel>), typeof(TestModel), typeof(TestModel));
            var record2 = new HandlersStoreRecord(typeof(IQsEventHandler<>), typeof(IQsEventHandler<TestModel>), typeof(TestModel), typeof(TestModel));

            _mockHandlerService.Setup(h => h.GetHandlers()).Returns(new[] { record1, record2 });
            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(It.IsAny<IConnection>(), It.IsAny<ChannelPurpose>(), It.IsAny<CancellationToken>())).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(It.IsAny<IChannel>(), It.IsAny<Type>(), It.IsAny<ExchangePurpose>())).ReturnsAsync("exchange");
            _mockQueueService.Setup(s => s.GetOrCreateQueuesAsync(It.IsAny<IChannel>(), It.IsAny<Type>(), It.IsAny<string>(), It.IsAny<QueuePurpose>())).ReturnsAsync("queue");
            _mockConsumerService.Setup(s => s.GetOrCreateConsumerAsync(It.IsAny<IChannel>(), It.IsAny<string>(), It.IsAny<HandlersStoreRecord>())).ReturnsAsync("tag");

            await _subscriber.SubscribeAsync();

            _mockConsumerService.Verify(s => s.GetOrCreateConsumerAsync(It.IsAny<IChannel>(), It.IsAny<string>(), It.IsAny<HandlersStoreRecord>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task SubscribeHandlerAsync_ForMessageHandler_UsesQueuePermanentChannel()
        {
            const string exchangeName = "exchange";
            const string queueName = "queue";
            var record = new HandlersStoreRecord(typeof(IQsMessageHandler<>), typeof(IQsMessageHandler<TestModel>), typeof(TestModel), typeof(TestModel));

            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.QueuePermanent, It.IsAny<CancellationToken>())).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestModel), ExchangePurpose.Permanent)).ReturnsAsync(exchangeName);
            _mockQueueService.Setup(s => s.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(TestModel), exchangeName, QueuePurpose.Permanent)).ReturnsAsync(queueName);
            _mockConsumerService.Setup(s => s.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record)).ReturnsAsync("tag");

            await _subscriber.SubscribeHandlerAsync(record);

            _mockChannelService.Verify(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.QueuePermanent, It.IsAny<CancellationToken>()), Times.Once);
            _mockExchangeService.Verify(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestModel), ExchangePurpose.Permanent), Times.Once);
            _mockQueueService.Verify(s => s.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(TestModel), exchangeName, QueuePurpose.Permanent), Times.Once);
            _mockConsumerService.Verify(s => s.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record), Times.Once);
        }

        [TestMethod]
        public async Task SubscribeHandlerAsync_ForEventHandler_UsesQueueConsumerTemporaryChannel()
        {
            const string exchangeName = "exchange";
            const string queueName = "queue";
            var record = new HandlersStoreRecord(typeof(IQsEventHandler<>), typeof(IQsEventHandler<TestModel>), typeof(TestModel), typeof(TestModel));

            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.QueueConsumerTemporary, It.IsAny<CancellationToken>())).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestModel), ExchangePurpose.Temporary)).ReturnsAsync(exchangeName);
            _mockQueueService.Setup(s => s.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(TestModel), exchangeName, QueuePurpose.ConsumerTemporary)).ReturnsAsync(queueName);
            _mockConsumerService.Setup(s => s.GetOrCreateConsumerAsync(_mockChannel.Object, queueName, record)).ReturnsAsync("tag");

            await _subscriber.SubscribeHandlerAsync(record);

            _mockChannelService.Verify(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.QueueConsumerTemporary, It.IsAny<CancellationToken>()), Times.Once);
            _mockExchangeService.Verify(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestModel), ExchangePurpose.Temporary), Times.Once);
            _mockQueueService.Verify(s => s.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(TestModel), exchangeName, QueuePurpose.ConsumerTemporary), Times.Once);
        }

        [TestMethod]
        public async Task SubscribeHandlerAsync_PassesQueueNameFromQueueServiceToConsumerService()
        {
            const string expectedQueueName = "specific-queue-name";
            var record = new HandlersStoreRecord(typeof(IQsMessageHandler<>), typeof(IQsMessageHandler<TestModel>), typeof(TestModel), typeof(TestModel));

            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(It.IsAny<IConnection>(), It.IsAny<ChannelPurpose>(), It.IsAny<CancellationToken>())).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(It.IsAny<IChannel>(), It.IsAny<Type>(), It.IsAny<ExchangePurpose>())).ReturnsAsync("exchange");
            _mockQueueService.Setup(s => s.GetOrCreateQueuesAsync(It.IsAny<IChannel>(), It.IsAny<Type>(), It.IsAny<string>(), It.IsAny<QueuePurpose>())).ReturnsAsync(expectedQueueName);
            _mockConsumerService.Setup(s => s.GetOrCreateConsumerAsync(It.IsAny<IChannel>(), expectedQueueName, record)).ReturnsAsync("tag");

            await _subscriber.SubscribeHandlerAsync(record);

            _mockConsumerService.Verify(s => s.GetOrCreateConsumerAsync(It.IsAny<IChannel>(), expectedQueueName, record), Times.Once);
        }
    }
}
