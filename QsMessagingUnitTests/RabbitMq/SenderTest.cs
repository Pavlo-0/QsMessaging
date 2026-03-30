using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Text.Json;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class SenderTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<Sender>> _mockLogger;
        private Mock<IConnectionService> _mockConnectionService;
        private Mock<IChannelService> _mockChannelService;
        private Mock<IExchangeService> _mockExchangeService;
        private Mock<IHandlerService> _mockHandlerService;
        private Mock<ISubscriber> _mockSubscriber;
        private Mock<IRequestResponseMessageStore> _mockMessageStore;
        private Mock<IConnection> _mockConnection;
        private Mock<IChannel> _mockChannel;
        private IRabbitMqSender _sender;
#pragma warning restore CS8618

        private class TestMessage { public string Name = string.Empty; }
        private class TestRequest { public string Name = string.Empty; }
        private class TestResponse { public string Name = string.Empty; }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<Sender>>();
            _mockConnectionService = new Mock<IConnectionService>();
            _mockChannelService = new Mock<IChannelService>();
            _mockExchangeService = new Mock<IExchangeService>();
            _mockHandlerService = new Mock<IHandlerService>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockMessageStore = new Mock<IRequestResponseMessageStore>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IChannel>();

            _sender = new Sender(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockChannelService.Object,
                _mockExchangeService.Object,
                _mockHandlerService.Object,
                new Lazy<ISubscriber>(() => _mockSubscriber.Object),
                _mockMessageStore.Object);
        }

        [TestMethod]
        public async Task SendMessageAsync_UsesPersistentDeliveryAndPermanentExchange()
        {
            const string exchangeName = "test-exchange";
            var model = new TestMessage { Name = "test" };

            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(CancellationToken.None)).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.MessagePublish, CancellationToken.None)).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestMessage), ExchangePurpose.Permanent)).ReturnsAsync(exchangeName);

            await _sender.SendMessageAsync(model);

            _mockChannel.Verify(c => c.BasicPublishAsync(
                exchangeName,
                string.Empty,
                true,
                It.Is<BasicProperties>(p => p.DeliveryMode == DeliveryModes.Persistent),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SendMessageAsync_WhenModelIsNull_ThrowsArgumentNullException()
        {
#pragma warning disable CS8625
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => _sender.SendMessageAsync<TestMessage>(null));
#pragma warning restore CS8625
        }

        [TestMethod]
        public async Task SendEventAsync_UsesTransientDeliveryAndTemporaryExchange()
        {
            const string exchangeName = "test-exchange";
            var model = new TestMessage { Name = "event" };

            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(CancellationToken.None)).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.EventPublish, CancellationToken.None)).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestMessage), ExchangePurpose.Temporary)).ReturnsAsync(exchangeName);

            await _sender.SendEventAsync(model);

            _mockChannel.Verify(c => c.BasicPublishAsync(
                exchangeName,
                string.Empty,
                false,  // mandatory = false for events
                It.Is<BasicProperties>(p => p.DeliveryMode == DeliveryModes.Transient && p.Expiration == "0"),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SendEventAsync_WhenModelIsNull_ThrowsArgumentNullException()
        {
#pragma warning disable CS8625
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => _sender.SendEventAsync<TestMessage>(null));
#pragma warning restore CS8625
        }

        [TestMethod]
        public async Task SendMessageCorrelationAsync_SetsCorrelationIdAndUsesTemporaryExchange()
        {
            const string exchangeName = "test-exchange";
            const string correlationId = "corr-123";
            var model = new TestMessage { Name = "response" };

            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(CancellationToken.None)).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.MessagePublish, CancellationToken.None)).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestMessage), ExchangePurpose.Temporary)).ReturnsAsync(exchangeName);

            await ((ISender)_sender).SendMessageCorrelationAsync(model, correlationId);

            _mockChannel.Verify(c => c.BasicPublishAsync(
                exchangeName,
                string.Empty,
                true,
                It.Is<BasicProperties>(p => p.DeliveryMode == DeliveryModes.Persistent && p.CorrelationId == correlationId),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SendRequest_CallsAllRequiredServicesAndReturnsResponse()
        {
            const string exchangeName = "test-exchange";
            var request = new TestRequest { Name = "req" };
            var expectedResponse = new TestResponse { Name = "resp" };
            var handlerRecord = new HandlersStoreRecord(typeof(object), typeof(object), typeof(object), typeof(object));

            _mockMessageStore.Setup(s => s.AddRequestMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockMessageStore.Setup(s => s.GetRespondedMessage<TestResponse>(It.IsAny<string>())).Returns(expectedResponse);
            _mockHandlerService.Setup(h => h.AddRRResponseHandler<TestResponse>()).Returns(handlerRecord);
            _mockSubscriber.Setup(s => s.SubscribeHandlerAsync(handlerRecord)).Returns(Task.CompletedTask);
            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(CancellationToken.None)).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.MessagePublish, CancellationToken.None)).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestRequest), ExchangePurpose.Temporary)).ReturnsAsync(exchangeName);

            var result = await _sender.SendRequest<TestRequest, TestResponse>(request, CancellationToken.None);

            Assert.AreEqual(expectedResponse.Name, result.Name);
            _mockMessageStore.Verify(s => s.AddRequestMessageAsync(It.IsAny<string>(), request, It.IsAny<CancellationToken>()), Times.Once);
            _mockHandlerService.Verify(h => h.AddRRResponseHandler<TestResponse>(), Times.Once);
            _mockSubscriber.Verify(s => s.SubscribeHandlerAsync(handlerRecord), Times.Once);
            _mockMessageStore.Verify(s => s.GetRespondedMessage<TestResponse>(It.IsAny<string>()), Times.Once);
            _mockMessageStore.Verify(s => s.RemoveMessage(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task SendRequest_PublishesSerializedBodyToExchange()
        {
            const string exchangeName = "test-exchange";
            var request = new TestRequest { Name = "req" };
            var handlerRecord = new HandlersStoreRecord(typeof(object), typeof(object), typeof(object), typeof(object));
            var expectedBodyLength = System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(request));

            _mockMessageStore.Setup(s => s.AddRequestMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockMessageStore.Setup(s => s.GetRespondedMessage<TestResponse>(It.IsAny<string>())).Returns(new TestResponse());
            _mockHandlerService.Setup(h => h.AddRRResponseHandler<TestResponse>()).Returns(handlerRecord);
            _mockSubscriber.Setup(s => s.SubscribeHandlerAsync(handlerRecord)).Returns(Task.CompletedTask);
            _mockConnectionService.Setup(s => s.GetOrCreateConnectionAsync(CancellationToken.None)).ReturnsAsync(_mockConnection.Object);
            _mockChannelService.Setup(s => s.GetOrCreateChannelAsync(_mockConnection.Object, ChannelPurpose.MessagePublish, CancellationToken.None)).ReturnsAsync(_mockChannel.Object);
            _mockExchangeService.Setup(s => s.GetOrCreateExchangeAsync(_mockChannel.Object, typeof(TestRequest), ExchangePurpose.Temporary)).ReturnsAsync(exchangeName);

            await _sender.SendRequest<TestRequest, TestResponse>(request, CancellationToken.None);

            _mockChannel.Verify(c => c.BasicPublishAsync(
                exchangeName,
                string.Empty,
                true,
                It.Is<BasicProperties>(p => !string.IsNullOrWhiteSpace(p.CorrelationId)),
                It.Is<ReadOnlyMemory<byte>>(b => b.Length == expectedBodyLength),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
