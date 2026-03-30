using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class ExchangeServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<ExchangeService>> _mockLogger;
        private Mock<INameGenerator> _mockNameGenerator;
        private Mock<IChannel> _mockChannel;
        private IExchangeService _exchangeService;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<ExchangeService>>();
            _mockNameGenerator = new Mock<INameGenerator>();
            _mockChannel = new Mock<IChannel>();

            _exchangeService = new ExchangeService(_mockLogger.Object, _mockNameGenerator.Object);

            // Reset static bag between tests
            var field = typeof(ExchangeService).GetField("storeExchangeRecords", BindingFlags.NonPublic | BindingFlags.Static);
            var bag = (ConcurrentBag<StoreExchangeRecord>)field!.GetValue(null)!;
            bag.Clear();
        }

        [TestMethod]
        public async Task GetOrCreateExchangeAsync_ReturnsExchangeNameFromNameGenerator()
        {
            const string exchangeName = "test-exchange";
            _mockNameGenerator.Setup(n => n.GetExchangeNameFromType(It.IsAny<Type>())).Returns(exchangeName);
            _mockChannel
                .Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _exchangeService.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(object), ExchangePurpose.Permanent);

            Assert.AreEqual(exchangeName, result);
        }

        [TestMethod]
        public async Task GetOrCreateExchangeAsync_WhenPermanentPurpose_DeclaresNonAutoDeleteExchange()
        {
            const string exchangeName = "test-exchange";
            _mockNameGenerator.Setup(n => n.GetExchangeNameFromType(It.IsAny<Type>())).Returns(exchangeName);
            _mockChannel
                .Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _exchangeService.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(object), ExchangePurpose.Permanent);

            _mockChannel.Verify(c => c.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Fanout,
                true,
                false,  // autoDelete = false for Permanent
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateExchangeAsync_WhenTemporaryPurpose_DeclaresAutoDeleteExchange()
        {
            const string exchangeName = "test-exchange";
            _mockNameGenerator.Setup(n => n.GetExchangeNameFromType(It.IsAny<Type>())).Returns(exchangeName);
            _mockChannel
                .Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _exchangeService.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(object), ExchangePurpose.Temporary);

            _mockChannel.Verify(c => c.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Fanout,
                true,
                true,   // autoDelete = true for Temporary
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreateExchangeAsync_WhenExchangeAlreadyExistsWithDifferentConfig_DoesNotThrow()
        {
            const string exchangeName = "test-exchange";
            _mockNameGenerator.Setup(n => n.GetExchangeNameFromType(It.IsAny<Type>())).Returns(exchangeName);

            var shutdownArgs = new ShutdownEventArgs(ShutdownInitiator.Peer, 406, "PRECONDITION_FAILED", (object?)null, CancellationToken.None);
            _mockChannel
                .Setup(c => c.ExchangeDeclareAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationInterruptedException(shutdownArgs));

            var result = await _exchangeService.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(object), ExchangePurpose.Permanent);

            Assert.AreEqual(exchangeName, result);
        }
    }
}
