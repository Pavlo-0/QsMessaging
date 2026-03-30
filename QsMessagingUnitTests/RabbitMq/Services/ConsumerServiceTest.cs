using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class ConsumerServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<ConsumerService>> _mockLogger;
        private Mock<ISender> _mockSender;
        private Mock<IServiceProvider> _mockServiceProvider;
        private Mock<IChannel> _mockChannel;
        private IConsumerService _consumerService;
#pragma warning restore CS8618

        private class TestModel { }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<ConsumerService>>();
            _mockSender = new Mock<ISender>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockChannel = new Mock<IChannel>();

            _consumerService = new ConsumerService(_mockLogger.Object, _mockSender.Object, _mockServiceProvider.Object);

            // Reset static bag between tests
            var field = typeof(ConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<StoreConsumerRecord>)field!.GetValue(null)!;
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
                    It.IsAny<IDictionary<string, object>>(),
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
        }

        [TestMethod]
        public async Task GetOrCreateConsumerAsync_WhenConsumerAlreadyRegistered_ReturnsExistingConsumerTagWithoutCreatingNew()
        {
            const string existingTag = "existing-tag";
            const string queueName = "test-queue";

            var field = typeof(ConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<StoreConsumerRecord>)field!.GetValue(null)!;
            bag.Add(new StoreConsumerRecord(_mockChannel.Object, queueName, existingTag));

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
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IAsyncBasicConsumer>(),
                It.IsAny<CancellationToken>()), Times.Never);
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

            var field = typeof(ConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<StoreConsumerRecord>)field!.GetValue(null)!;
            bag.Add(new StoreConsumerRecord(_mockChannel.Object, "queue1", consumerTag));

            var result = _consumerService.GetConsumersByChannel(_mockChannel.Object).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(consumerTag, result[0]);
        }

        [TestMethod]
        public void GetConsumersByChannel_WhenConsumersExistForDifferentChannel_ReturnsEmpty()
        {
            var otherChannel = new Mock<IChannel>();

            var field = typeof(ConsumerService).GetField("storeConsumerRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<StoreConsumerRecord>)field!.GetValue(null)!;
            bag.Add(new StoreConsumerRecord(otherChannel.Object, "queue1", "tag1"));

            var result = _consumerService.GetConsumersByChannel(_mockChannel.Object);

            Assert.IsFalse(result.Any());
        }
    }
}
