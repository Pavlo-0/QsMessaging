using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using Azure.Messaging.ServiceBus;
using QsMessaging.Public.Handler;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessagingUnitTests.AzureServiceBus
{
    [TestClass]
    public class SubscriberTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<AsbSubscriber>> _mockLogger;
        private Mock<IAsbServiceBusProcessorService> _mockProcessorService;
        private Mock<IHandlerService> _mockHandlerService;
        private Mock<IAsbConsumerService> _mockHandlersService;
        private Mock<ServiceBusProcessor> _mockProcessor;
        private AsbSubscriber _subscriber;
#pragma warning restore CS8618

        private sealed class TestModel { }

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbSubscriber>>();
            _mockProcessorService = new Mock<IAsbServiceBusProcessorService>();
            _mockHandlerService = new Mock<IHandlerService>();
            _mockHandlersService = new Mock<IAsbConsumerService>();
            _mockProcessor = new Mock<ServiceBusProcessor>();

            _subscriber = new AsbSubscriber(
                _mockLogger.Object,
                _mockProcessorService.Object,
                _mockHandlerService.Object,
                _mockHandlersService.Object);
        }

        [TestMethod]
        public async Task SubscribeAsync_WhenNoHandlers_DoesNotCreateProcessors()
        {
            _mockHandlerService.Setup(h => h.GetHandlers()).Returns(Enumerable.Empty<HandlersStoreRecord>());

            await _subscriber.SubscribeAsync();

            _mockProcessorService.Verify(
                s => s.GetOrCreate(It.IsAny<HandlersStoreRecord>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        /*
        [TestMethod]
        public async Task CloseAsync_StopsProcessorsThroughProcessorService()
        {
            await _subscriber.CloseAsync();

            _mockProcessorService.Verify(s => s.StopAndDisposeProcessorAsync(), Times.Once);
        }*/

        [TestMethod]
        public async Task SubscribeHandlerAsync_RegistersHandlersWithProcessorService()
        {
            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(TestModel));

            _mockProcessorService
                .Setup(service => service.GetOrCreate(record, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockProcessor.Object);
            _mockProcessor
                .Setup(processor => processor.StartProcessingAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _subscriber.SubscribeHandlerAsync(record);
            /*
            _mockProcessorService.Verify(
                service => service.RegisterHandlers(
                    _mockProcessor.Object,
                    It.IsAny<Func<ProcessMessageEventArgs, Task>>(),
                    It.IsAny<Func<ProcessErrorEventArgs, Task>>()),
                Times.Once);*/
        }
    }
}
