using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessagingUnitTests.AzureServiceBus
{
    [TestClass]
    public class SubscriberTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<AsbSubscriber>> _mockLogger;
        private Mock<IAbsConnectionService> _mockConnectionService;
        private Mock<IAdministrationService> _mockAdministrationService;
        private Mock<IQueueAdministration> _mockQueueAdministration;
        private Mock<ISubscriptionService> _mockSubscriptionService;
        private Mock<IHandlerService> _mockHandlerService;
        private Mock<IServiceProvider> _mockServiceProvider;
        private Mock<ISender> _mockResponseSender;
        private AsbSubscriber _subscriber;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbSubscriber>>();
            _mockConnectionService = new Mock<IAbsConnectionService>();
            _mockAdministrationService = new Mock<IAdministrationService>();
            _mockQueueAdministration = new Mock<IQueueAdministration>();
            _mockSubscriptionService = new Mock<ISubscriptionService>();
            _mockHandlerService = new Mock<IHandlerService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockResponseSender = new Mock<ISender>();

            _subscriber = new AsbSubscriber(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockAdministrationService.Object,
                _mockQueueAdministration.Object,
                _mockSubscriptionService.Object,
                _mockHandlerService.Object,
                _mockServiceProvider.Object,
                _mockResponseSender.Object);
        }

        [TestMethod]
        public async Task CloseAsync_WhenProcessorWasAlreadyDisposed_DoesNotThrow()
        {
            var processors = GetProcessors();
            processors["processor"] = new ThrowingProcessor();

            await _subscriber.CloseAsync();

            Assert.AreEqual(0, processors.Count);
        }

        private System.Collections.Concurrent.ConcurrentDictionary<string, ServiceBusProcessor> GetProcessors()
        {
            var field = typeof(AsbSubscriber).GetField("_processors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Unable to find processor store.");
            return (System.Collections.Concurrent.ConcurrentDictionary<string, ServiceBusProcessor>)field.GetValue(_subscriber)!;
        }

        private sealed class ThrowingProcessor : ServiceBusProcessor
        {
            public override bool IsClosed => false;

            public override bool IsProcessing => true;

            public override Task StopProcessingAsync(CancellationToken cancellationToken = default)
            {
                throw new ObjectDisposedException("System.Threading.SemaphoreSlim");
            }
        }
    }
}
