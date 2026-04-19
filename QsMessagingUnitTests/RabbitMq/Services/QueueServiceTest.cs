using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class QueueServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RqQueueService>> _mockLogger;
        private Mock<IRqNameGenerator> _mockNameGenerator;
        private Mock<IChannel> _mockChannel;
        private IRqQueueService _queueService;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RqQueueService>>();
            _mockNameGenerator = new Mock<IRqNameGenerator>();
            _mockChannel = new Mock<IChannel>();

            _queueService = new RqQueueService(_mockLogger.Object, _mockNameGenerator.Object);

            // Reset static bag between tests
            var field = typeof(RqQueueService).GetField("storeQueueRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<RqStoreQueueRecord>)field!.GetValue(null)!;
            bag.Clear();
        }

        [TestMethod]
        public async Task GetOrCreateQueuesAsync_ReturnsQueueNameFromNameGenerator()
        {
            const string queueName = "test-queue";
            _mockNameGenerator.Setup(n => n.GetQueueNameFromType(It.IsAny<Type>(), It.IsAny<RqQueuePurpose>())).Returns(queueName);
            _mockChannel
                .Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));
            _mockChannel
                .Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _queueService.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(object), "exchange", RqQueuePurpose.Permanent);

            Assert.AreEqual(queueName, result);
        }

        [TestMethod]
        public async Task GetOrCreateQueuesAsync_WhenPermanentPurpose_DeclaresNonAutoDeleteQueue()
        {
            const string queueName = "test-queue";
            _mockNameGenerator.Setup(n => n.GetQueueNameFromType(It.IsAny<Type>(), It.IsAny<RqQueuePurpose>())).Returns(queueName);
            _mockChannel
                .Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));
            _mockChannel
                .Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _queueService.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(object), "exchange", RqQueuePurpose.Permanent);

            _mockChannel.Verify(c => c.QueueDeclareAsync(
                queueName,
                true,   // durable
                false,  // exclusive
                false,  // autoDelete = false for Permanent
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateQueuesAsync_WhenConsumerTemporaryPurpose_DeclaresAutoDeleteQueue()
        {
            const string queueName = "test-queue";
            _mockNameGenerator.Setup(n => n.GetQueueNameFromType(It.IsAny<Type>(), It.IsAny<RqQueuePurpose>())).Returns(queueName);
            _mockChannel
                .Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));
            _mockChannel
                .Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _queueService.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(object), "exchange", RqQueuePurpose.ConsumerTemporary);

            _mockChannel.Verify(c => c.QueueDeclareAsync(
                queueName,
                true,   // durable
                false,  // exclusive
                true,   // autoDelete = true for ConsumerTemporary
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateQueuesAsync_WhenInstanceTemporaryPurpose_DeclaresAutoDeleteQueue()
        {
            const string queueName = "test-queue";
            _mockNameGenerator.Setup(n => n.GetQueueNameFromType(It.IsAny<Type>(), It.IsAny<RqQueuePurpose>())).Returns(queueName);
            _mockChannel
                .Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));
            _mockChannel
                .Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _queueService.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(object), "exchange", RqQueuePurpose.InstanceTemporary);

            _mockChannel.Verify(c => c.QueueDeclareAsync(
                queueName,
                true,
                false,
                true,   // autoDelete = true for InstanceTemporary
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateQueuesAsync_WhenPermanentAndQueueBindThrows_DoesNotRethrow()
        {
            const string queueName = "test-queue";
            _mockNameGenerator.Setup(n => n.GetQueueNameFromType(It.IsAny<Type>(), It.IsAny<RqQueuePurpose>())).Returns(queueName);
            _mockChannel
                .Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));

            var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Peer, 406, "PRECONDITION_FAILED", (object?)null, CancellationToken.None);
            _mockChannel
                .Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationInterruptedException(shutdownArgs));

            var result = await _queueService.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(object), "exchange", RqQueuePurpose.Permanent);

            Assert.AreEqual(queueName, result);
        }

        [TestMethod]
        public async Task GetOrCreateQueuesAsync_WhenConsumerTemporaryAndQueueBindThrows_Rethrows()
        {
            const string queueName = "test-queue";
            _mockNameGenerator.Setup(n => n.GetQueueNameFromType(It.IsAny<Type>(), It.IsAny<RqQueuePurpose>())).Returns(queueName);
            _mockChannel
                .Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));

            var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Peer, 406, "PRECONDITION_FAILED", (object?)null, CancellationToken.None);
            _mockChannel
                .Setup(c => c.QueueBindAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationInterruptedException(shutdownArgs));

            await Assert.ThrowsExceptionAsync<OperationInterruptedException>(
                () => _queueService.GetOrCreateQueuesAsync(_mockChannel.Object, typeof(object), "exchange", RqQueuePurpose.ConsumerTemporary));
        }
    }
}
