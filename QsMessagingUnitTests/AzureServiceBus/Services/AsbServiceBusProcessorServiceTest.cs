using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public.Handler;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models;

namespace QsMessagingUnitTests.AzureServiceBus.Services
{
    [TestClass]
    public class AsbServiceBusProcessorServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<AsbServiceBusProcessorService>> _mockLogger;
        private Mock<IAsbConnectionService> _mockConnectionService;
        private Mock<IAsbTopicService> _mockTopicService;
        private Mock<IAsbQueueService> _mockQueueService;
        private Mock<IAsbTopicSubscriptionService> _mockTopicSubscriptionService;
        private Mock<ServiceBusClient> _mockConnection;
        private Mock<ServiceBusProcessor> _mockProcessor;
        private AsbServiceBusProcessorService _service;
#pragma warning restore CS8618

        private sealed class TestRequest { }

        private sealed class TestResponse { }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbServiceBusProcessorService>>();
            _mockConnectionService = new Mock<IAsbConnectionService>();
            _mockTopicService = new Mock<IAsbTopicService>();
            _mockQueueService = new Mock<IAsbQueueService>();
            _mockTopicSubscriptionService = new Mock<IAsbTopicSubscriptionService>();
            _mockConnection = new Mock<ServiceBusClient>();
            _mockProcessor = new Mock<ServiceBusProcessor>();

            _mockConnectionService
                .Setup(service => service.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);

            _service = new AsbServiceBusProcessorService(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockTopicService.Object,
                _mockQueueService.Object,
                _mockTopicSubscriptionService.Object);
        }

        [TestMethod]
        public async Task GetOrCreate_ForRequestHandler_CreatesRequestQueueProcessor()
        {
            ServiceBusProcessorOptions? options = null;
            var record = new HandlersStoreRecord(
                typeof(IQsRequestResponseHandler<,>),
                typeof(IQsRequestResponseHandler<TestRequest, TestResponse>),
                typeof(IQsRequestResponseHandler<TestRequest, TestResponse>),
                typeof(TestRequest));

            _mockQueueService
                .Setup(service => service.GetOrCreateQueueAsync(typeof(TestRequest), AsbQueuePurpose.Request, It.IsAny<CancellationToken>()))
                .ReturnsAsync("request-queue");
            _mockConnection
                .Setup(connection => connection.CreateProcessor("request-queue", It.IsAny<ServiceBusProcessorOptions>()))
                .Callback<string, ServiceBusProcessorOptions>((_, processorOptions) => options = processorOptions)
                .Returns(_mockProcessor.Object);

            var processor = await _service.GetOrCreate(record, CancellationToken.None);

            Assert.AreSame(_mockProcessor.Object, processor);
            Assert.IsNotNull(options);
            Assert.IsFalse(options.AutoCompleteMessages);
            Assert.AreEqual(1, options.MaxConcurrentCalls);
            _mockQueueService.Verify(
                service => service.GetOrCreateQueueAsync(typeof(TestRequest), AsbQueuePurpose.Request, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreate_ForResponseHandler_CreatesResponseQueueProcessor()
        {
            var record = new HandlersStoreRecord(
                typeof(IRRResponseHandler),
                typeof(IRRResponseHandler),
                typeof(IRRResponseHandler),
                typeof(TestResponse));

            _mockQueueService
                .Setup(service => service.GetOrCreateQueueAsync(typeof(TestResponse), AsbQueuePurpose.Response, It.IsAny<CancellationToken>()))
                .ReturnsAsync("response-queue");
            _mockConnection
                .Setup(connection => connection.CreateProcessor("response-queue", It.IsAny<ServiceBusProcessorOptions>()))
                .Returns(_mockProcessor.Object);

            var processor = await _service.GetOrCreate(record, CancellationToken.None);

            Assert.AreSame(_mockProcessor.Object, processor);
            _mockQueueService.Verify(
                service => service.GetOrCreateQueueAsync(typeof(TestResponse), AsbQueuePurpose.Response, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task GetOrCreate_ForMessageHandler_CreatesTopicSubscriptionProcessor()
        {
            ServiceBusProcessorOptions? options = null;
            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestRequest>),
                typeof(IQsMessageHandler<TestRequest>),
                typeof(TestRequest));

            _mockTopicService
                .Setup(service => service.GetOrCreateTopicAsync(typeof(TestRequest), It.IsAny<CancellationToken>()))
                .ReturnsAsync("topic");
            _mockTopicSubscriptionService
                .Setup(service => service.GetOrCreateSubscriptionAsync(record, It.IsAny<CancellationToken>()))
                .ReturnsAsync("subscription");
            _mockConnection
                .Setup(connection => connection.CreateProcessor("topic", "subscription", It.IsAny<ServiceBusProcessorOptions>()))
                .Callback<string, string, ServiceBusProcessorOptions>((_, _, processorOptions) => options = processorOptions)
                .Returns(_mockProcessor.Object);

            var processor = await _service.GetOrCreate(record, CancellationToken.None);

            Assert.AreSame(_mockProcessor.Object, processor);
            Assert.IsNotNull(options);
            Assert.IsFalse(options.AutoCompleteMessages);
            Assert.AreEqual(1, options.MaxConcurrentCalls);
            _mockTopicService.Verify(
                service => service.GetOrCreateTopicAsync(typeof(TestRequest), It.IsAny<CancellationToken>()),
                Times.Once);
            _mockTopicSubscriptionService.Verify(
                service => service.GetOrCreateSubscriptionAsync(record, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
