using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessagingUnitTests.AzureServiceBus
{
    [TestClass]
    public class SenderTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<AsbSender>> _mockLogger;
        private Mock<IAsbConnectionService> _mockConnectionService;
        private Mock<IAsbTopicService> _mockTopicService;
        private Mock<IAsbQueueService> _mockQueueService;
        private Mock<IQsMessagingConfiguration> _mockConfiguration;
        private Mock<IHandlerService> _mockHandlerService;
        private Mock<ISubscriber> _mockSubscriber;
        private Mock<IRequestResponseMessageStore> _mockMessageStore;
        private AsbSender _sender;
#pragma warning restore CS8618

        private sealed class TestMessage
        {
            public string Name { get; set; } = "";
        }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbSender>>();
            _mockConnectionService = new Mock<IAsbConnectionService>();
            _mockTopicService = new Mock<IAsbTopicService>();
            _mockQueueService = new Mock<IAsbQueueService>();
            _mockConfiguration = new Mock<IQsMessagingConfiguration>();
            _mockHandlerService = new Mock<IHandlerService>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockMessageStore = new Mock<IRequestResponseMessageStore>();

            _mockTopicService
                .Setup(x => x.GetOrCreateTopicAsync(typeof(TestMessage), It.IsAny<CancellationToken>()))
                .ReturnsAsync("topic");
            _mockConfiguration
                .SetupGet(x => x.Resilience)
                .Returns(new QsMessageReceiverRetryConfiguration
                {
                    MaxRetryAttempts = 0,
                    Delay = TimeSpan.Zero
                });

            _sender = new AsbSender(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockTopicService.Object,
                _mockQueueService.Object,
                _mockConfiguration.Object,
                _mockHandlerService.Object,
                new Lazy<ISubscriber>(() => _mockSubscriber.Object),
                _mockMessageStore.Object);
        }

        [TestMethod]
        public async Task SendMessageAsync_WhenSendReportsMissingEntity_RetriesThenLogsWarning()
        {
            var mockClient = new Mock<ServiceBusClient>();
            var mockSender = new Mock<ServiceBusSender>();

            _mockConfiguration
                .SetupGet(x => x.Resilience)
                .Returns(new QsMessageReceiverRetryConfiguration
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero
                });
            _mockConnectionService
                .Setup(x => x.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);
            mockClient
                .Setup(x => x.CreateSender("topic"))
                .Returns(mockSender.Object);
            mockSender
                .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ServiceBusException(
                    "missing entity",
                    ServiceBusFailureReason.MessagingEntityNotFound,
                    "topic"));

            await _sender.SendMessageAsync(new TestMessage { Name = "missing" });

            mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("was not published")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task SendMessageAsync_WhenPermanentSubscriptionExists_SendsMessageWithMessageTtl()
        {
            var mockClient = new Mock<ServiceBusClient>();
            var mockSender = new Mock<ServiceBusSender>();
            ServiceBusMessage? sentMessage = null;

            _mockConnectionService
                .Setup(x => x.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockClient.Object);
            mockClient
                .Setup(x => x.CreateSender("topic"))
                .Returns(mockSender.Object);
            mockSender
                .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
                .Callback<ServiceBusMessage, CancellationToken>((message, _) => sentMessage = message)
                .Returns(Task.CompletedTask);

            await _sender.SendMessageAsync(new TestMessage { Name = "ready" });

            Assert.IsNotNull(sentMessage);
            Assert.AreEqual(TimeSpan.FromDays(14), sentMessage.TimeToLive);
            mockSender.Verify(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
