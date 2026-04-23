using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using System.Reflection;

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
        private AsbServiceBusProcessorService _service;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbServiceBusProcessorService>>();
            _mockConnectionService = new Mock<IAsbConnectionService>();
            _mockTopicService = new Mock<IAsbTopicService>();
            _mockQueueService = new Mock<IAsbQueueService>();
            _mockTopicSubscriptionService = new Mock<IAsbTopicSubscriptionService>();

            _service = new AsbServiceBusProcessorService(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockTopicService.Object,
                _mockQueueService.Object,
                _mockTopicSubscriptionService.Object);

            ClearProcessors();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClearProcessors();
        }
        /*
        [TestMethod]
        public async Task StopAndDisposeProcessorAsync_WhenProcessorIsRunning_StopsProcessing()
        {
            var processorMock = new Mock<ServiceBusProcessor>();
            var stopCalled = false;

            processorMock.SetupGet(processor => processor.IsClosed).Returns(false);
            processorMock.SetupGet(processor => processor.IsProcessing).Returns(true);
            processorMock
                .Setup(processor => processor.StopProcessingAsync(It.IsAny<CancellationToken>()))
                .Callback(() => stopCalled = true)
                .Returns(Task.CompletedTask);

            _service.RegisterHandlers(
                processorMock.Object,
                _ => Task.CompletedTask,
                _ => Task.CompletedTask);

            await _service.StopAndDisposeProcessorAsync();

            Assert.IsTrue(stopCalled);
        }*/

        private static void ClearProcessors()
        {
            var field = typeof(AsbServiceBusProcessorService).GetField("_processors", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Processor store field was not found.");
            var value = field.GetValue(null)
                ?? throw new InvalidOperationException("Processor store value was not found.");
            var clearMethod = value.GetType().GetMethod("Clear")
                ?? throw new InvalidOperationException("Processor store clear method was not found.");

            clearMethod.Invoke(value, null);
        }
    }
}
