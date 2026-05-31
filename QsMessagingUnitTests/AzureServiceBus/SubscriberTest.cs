using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using Azure.Messaging.ServiceBus;
using QsMessaging.Public.Handler;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Reflection;

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
            _mockProcessor.SetupGet(processor => processor.Identifier).Returns("processor-1");
            _mockProcessor
                .Setup(processor => processor.StartProcessingAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockProcessor
                .Setup(processor => processor.StopProcessingAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            ClearProcessors();

            _subscriber = new AsbSubscriber(
                _mockLogger.Object,
                _mockProcessorService.Object,
                _mockHandlerService.Object,
                _mockHandlersService.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ClearProcessors();
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

        [TestMethod]
        public async Task CloseAsync_WhenProcessorsExist_CancelsStoredTokenAndStopsProcessors()
        {
            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(IQsMessageHandler<TestModel>),
                typeof(TestModel));

            _mockProcessor.SetupGet(processor => processor.IsClosed).Returns(false);
            _mockProcessor.SetupGet(processor => processor.IsProcessing).Returns(true);
            _mockProcessorService
                .Setup(service => service.GetOrCreate(record, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AsbProcessorRegistration(_mockProcessor.Object, "entity", "topic", "entity"));

            await _subscriber.SubscribeHandlerAsync(record);

            var processorCancellationToken = GetStoredProcessorCancellationToken();

            await _subscriber.CloseAsync();

            Assert.IsTrue(processorCancellationToken.IsCancellationRequested);
            _mockProcessor.Verify(processor => processor.StopProcessingAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

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
                .ReturnsAsync(new AsbProcessorRegistration(_mockProcessor.Object, "entity", "topic", "entity"));

            await _subscriber.SubscribeHandlerAsync(record);

            _mockProcessor.Verify(
                processor => processor.StartProcessingAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static void ClearProcessors()
        {
            var field = typeof(AsbSubscriber).GetField("_processors", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Processor store field was not found.");
            var value = field.GetValue(null)
                ?? throw new InvalidOperationException("Processor store value was not found.");
            var clearMethod = value.GetType().GetMethod("Clear")
                ?? throw new InvalidOperationException("Processor store clear method was not found.");

            clearMethod.Invoke(value, null);
        }

        private static CancellationToken GetStoredProcessorCancellationToken()
        {
            var field = typeof(AsbSubscriber).GetField("_processors", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Processor store field was not found.");
            var value = field.GetValue(null) as System.Collections.IEnumerable
                ?? throw new InvalidOperationException("Processor store value was not enumerable.");
            var processorRecord = value.Cast<object>().Single();
            var cancellationTokenSource = processorRecord
                .GetType()
                .GetProperty("CancellationTokenSource", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(processorRecord) as CancellationTokenSource
                ?? throw new InvalidOperationException("Processor cancellation token source was not found.");

            return cancellationTokenSource.Token;
        }
    }
}
