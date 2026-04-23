using Moq;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using QsMessaging.Shared.Interface;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared.Services.Interfaces;
using QsMessaging.Shared.Models;
using QsMessaging.Public.Handler;

namespace QsMessaging.Tests
{
    [TestClass]
    public class SenderTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private Mock<ILogger<RqSender>> _mockLogger;
        private Mock<IRqConnectionService> _mockConnectionService;
        private Mock<IRqChannelService> _mockChannelService;
        private Mock<IRqExchangeService> _mockExchangeService;
        private Mock<IHandlerService> _mockHandlerService;
        private Mock<ISubscriber> _mockSubscriber;
        private Mock<IRequestResponseMessageStore> _mockMessageStore;
        private RqSender _sender;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RqSender>>();
            _mockConnectionService = new Mock<IRqConnectionService>();
            _mockChannelService = new Mock<IRqChannelService>();
            _mockExchangeService = new Mock<IRqExchangeService>();
            _mockHandlerService = new Mock<IHandlerService>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockMessageStore = new Mock<IRequestResponseMessageStore>();

            _sender = new RqSender(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockChannelService.Object,
                _mockExchangeService.Object,
                _mockHandlerService.Object,
                new Lazy<ISubscriber>(() => _mockSubscriber.Object),
                _mockMessageStore.Object);
        }

        [TestMethod]
        public async Task SendMessageAsync_ShouldSendPersistentMessage()
        {
            // Arrange
            var model = new { Name = "TestMessage" };
            var modelType = model.GetType();
            var connection = new Mock<IConnection>();
            var channel = new Mock<IChannel>();
            var exchangeName = "TestExchange";

            _mockConnectionService.Setup(x => x.GetOrCreateConnectionAsync(CancellationToken.None))
                .ReturnsAsync(connection.Object);

            _mockChannelService.Setup(x => x.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, CancellationToken.None))
                .ReturnsAsync(channel.Object);

            _mockExchangeService.Setup(x => x.GetOrCreateExchangeAsync(channel.Object, modelType, RqExchangePurpose.Permanent, CancellationToken.None))
                .ReturnsAsync(exchangeName);

            // Act
            await _sender.SendMessageAsync(model);

            // Assert
            _mockExchangeService.Verify(x => x.GetOrCreateExchangeAsync(channel.Object, modelType, RqExchangePurpose.Permanent, CancellationToken.None), Times.Once);
            _mockChannelService.Verify(GetType => GetType.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, CancellationToken.None), Times.Once);
            _mockConnectionService.Verify(x => x.GetOrCreateConnectionAsync(CancellationToken.None), Times.Once);

            channel.Verify(x => x.BasicPublishAsync(
                exchangeName,
                string.Empty,
                true,
                It.Is<BasicProperties>(props => props.DeliveryMode == DeliveryModes.Persistent),
                It.Is<ReadOnlyMemory<byte>>(p => p.Length == 22),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SendMessageAsync_ShouldThrowException_WhenModelIsNull()
        {
            // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            await _sender.SendMessageAsync<object>(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [TestMethod]
        public async Task SendMessageCorrelationAsync_ShouldSendMessageWithCorrelationId()
        {
            // Arrange
            var model = new { Data = "Test" };
            var correlationId = Guid.NewGuid().ToString();
            var connection = new Mock<IConnection>();
            var channel = new Mock<IChannel>();
            var replyExchangeName = "TestExchange";

            _mockConnectionService.Setup(x => x.GetOrCreateConnectionAsync(CancellationToken.None)).ReturnsAsync(connection.Object);
            _mockChannelService.Setup(x => x.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, CancellationToken.None)).ReturnsAsync(channel.Object);

            // Act
            await _sender.SendMessageCorrelatedAsync(model, correlationId, replyExchangeName);

            // Assert
            _mockExchangeService.Verify(x => x.GetOrCreateExchangeAsync(It.IsAny<IChannel>(), It.IsAny<Type>(), It.IsAny<RqExchangePurpose>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockChannelService.Verify(x => x.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, CancellationToken.None), Times.Once);
            _mockConnectionService.Verify(x => x.GetOrCreateConnectionAsync(CancellationToken.None), Times.Once);
            channel.Verify(x => x.BasicPublishAsync(
                replyExchangeName,
                string.Empty,
                true,
                It.Is<BasicProperties>(props => props.DeliveryMode == DeliveryModes.Persistent &&  props.CorrelationId == correlationId),
                It.Is<ReadOnlyMemory<byte>>(p => p.Length == 15 && 
                p.ToArray()[0] == 123 && 
                p.ToArray()[1] == 34 &&
                p.ToArray()[2] == 68 &&
                p.ToArray()[3] == 97 &&
                p.ToArray()[4] == 116 &&
                p.ToArray()[5] == 97 &&
                p.ToArray()[6] == 34 &&
                p.ToArray()[7] == 58 &&
                p.ToArray()[8] == 34 &&
                p.ToArray()[9] == 84 &&
                p.ToArray()[10] == 101 &&
                p.ToArray()[11] == 115 &&
                p.ToArray()[12] == 116 &&
                p.ToArray()[13] == 34 &&
                p.ToArray()[14] == 125 ),
                It.IsAny<CancellationToken>()), 
             Times.Once);
        }

        [TestMethod]
        public async Task SendRequest_ShouldReturnResponse_WhenResponseReceived()
        {
            // Arrange
            var requestModel = new RequestModel { Name = "Request" };
            var responseModel = new ResponseModel { Name = "Response" };
            var responseAssertModel = new ResponseModel { Name = "Response" };
            var connection = new Mock<IConnection>();
            var publishChannel = new Mock<IChannel>();
            var responseChannel = new Mock<IChannel>();
            var requestExchangeName = "RequestExchange";
            var responseExchangeName = "ResponseExchange";
            var handlerRecord = new HandlersStoreRecord(
                typeof(IRRResponseHandler),
                typeof(IRRResponseHandler),
                typeof(object),
                typeof(ResponseModel));


            _mockMessageStore.Setup(x => x.AddRequestMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockMessageStore.Setup(x => x.GetRespondedMessage<ResponseModel>(It.IsAny<string>()))
                .Returns(responseModel);

            _mockHandlerService.Setup(x => x.AddRRResponseHandler<ResponseModel>())
                .Returns((handlerRecord, true));

            _mockSubscriber.Setup(x => x.SubscribeHandlerAsync(It.IsAny<HandlersStoreRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockConnectionService.Setup(x => x.GetOrCreateConnectionAsync(CancellationToken.None))
                .ReturnsAsync(connection.Object);

            _mockChannelService.Setup(x => x.GetOrCreateChannelAsync(RqChannelPurpose.QueueInstanceTemporary, CancellationToken.None))
                .ReturnsAsync(responseChannel.Object);
            _mockChannelService.Setup(x => x.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, CancellationToken.None))
                .ReturnsAsync(publishChannel.Object);

            _mockExchangeService.Setup(x => x.GetOrCreateExchangeAsync(responseChannel.Object, typeof(ResponseModel), RqExchangePurpose.TemporaryForResponse, CancellationToken.None))
                .ReturnsAsync(responseExchangeName);
            _mockExchangeService.Setup(x => x.GetOrCreateExchangeAsync(publishChannel.Object, requestModel.GetType(), RqExchangePurpose.Temporary, CancellationToken.None))
                .ReturnsAsync(requestExchangeName);


            // Act
            var response = await _sender.SendRequest<RequestModel, ResponseModel>(requestModel);

            // Assert
            _mockExchangeService.Verify(x => x.GetOrCreateExchangeAsync(responseChannel.Object, typeof(ResponseModel), RqExchangePurpose.TemporaryForResponse, CancellationToken.None), Times.Once);
            _mockExchangeService.Verify(x => x.GetOrCreateExchangeAsync(publishChannel.Object, requestModel.GetType(), RqExchangePurpose.Temporary, CancellationToken.None), Times.Once);
            _mockChannelService.Verify(x => x.GetOrCreateChannelAsync(RqChannelPurpose.QueueInstanceTemporary, CancellationToken.None), Times.Once);
            _mockChannelService.Verify(x => x.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, CancellationToken.None), Times.Once);
            _mockConnectionService.Verify(x => x.GetOrCreateConnectionAsync(CancellationToken.None), Times.Exactly(2));
            _mockSubscriber.Verify(x => x.SubscribeHandlerAsync(It.IsAny<HandlersStoreRecord>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockHandlerService.Verify(x => x.AddRRResponseHandler<ResponseModel>(), Times.Once);
            _mockHandlerService.Verify(x => x.AddRRResponseHandler<ResponseModel>(), Times.Once);
            _mockMessageStore.Verify(x => x.AddRequestMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockMessageStore.Verify(x => x.GetRespondedMessage<ResponseModel>(It.IsAny<string>()), Times.Once);

            Assert.IsNotNull(response);
            Assert.AreEqual(responseAssertModel.Name, response.Name);
            publishChannel.Verify(x => x.BasicPublishAsync(
              requestExchangeName,
              string.Empty,
              true,
              It.Is<BasicProperties>(props =>
                  props.DeliveryMode == DeliveryModes.Persistent &&
                  !string.IsNullOrWhiteSpace(props.CorrelationId) &&
                  props.ReplyTo == responseExchangeName),
              It.IsAny<ReadOnlyMemory<byte>>(),
              It.IsAny<CancellationToken>()),
              Times.Once);
        }

        [TestMethod]
        public async Task SendEventAsync_ShouldSendTransientEvent()
        {
            // Arrange
            var eventModel = new { EventData = "TestEvent" };
            var eventModelType = eventModel.GetType();
            var connection = new Mock<IConnection>();
            var channel = new Mock<IChannel>();
            var exchangeName = "TestExchange";

            _mockConnectionService.Setup(x => x.GetOrCreateConnectionAsync(CancellationToken.None))
                .ReturnsAsync(connection.Object);

            _mockChannelService.Setup(x => x.GetOrCreateChannelAsync(RqChannelPurpose.EventPublish, CancellationToken.None))
                .ReturnsAsync(channel.Object);

            _mockExchangeService.Setup(x => x.GetOrCreateExchangeAsync(channel.Object, eventModelType, RqExchangePurpose.Temporary, CancellationToken.None))
                .ReturnsAsync(exchangeName);

            // Act
            await _sender.SendEventAsync(eventModel);


            // Assert

            _mockExchangeService.Verify(x => x.GetOrCreateExchangeAsync(channel.Object, eventModelType, RqExchangePurpose.Temporary, CancellationToken.None), Times.Once);
            _mockChannelService.Verify(x => x.GetOrCreateChannelAsync(RqChannelPurpose.EventPublish, CancellationToken.None), Times.Once);
            _mockConnectionService.Verify(x => x.GetOrCreateConnectionAsync(CancellationToken.None), Times.Once);

            channel.Verify(x => x.BasicPublishAsync(
                exchangeName,
                string.Empty,
                false,
                It.Is<BasicProperties>(props => string.IsNullOrEmpty(props.Expiration) && props.DeliveryMode == DeliveryModes.Transient),
                It.Is<ReadOnlyMemory<byte>>(p=> p.Length == 25),
                It.IsAny<CancellationToken>()), 
              Times.Once);
        }

        private class RequestModel
        {
            public string Name { get; set; } = "";
        }

        private class ResponseModel
        {
            public string Name { get; set; } = "";
        }
    }
}
