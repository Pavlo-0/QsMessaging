using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Services.Interfaces;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class RqTransportFullCleanerTest
    {
        [TestMethod]
        public async Task FullCleanUp_WhenDangerousCleanupDisabled_DeletesOnlyQsPrefixedEntities()
        {
            var managementService = new Mock<IRqManagementService>(MockBehavior.Strict);
            managementService
                .Setup(service => service.GetQueueNamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "Qs:app:queue", "external.queue" });
            managementService
                .Setup(service => service.GetExchangeNamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "Qs:app:exchange", "external.exchange", "amq.direct" });
            managementService
                .Setup(service => service.DeleteQueueAsync("Qs:app:queue", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            managementService
                .Setup(service => service.DeleteExchangeAsync("Qs:app:exchange", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var cleaner = CreateCleaner(managementService.Object, allowDangerousFullCleanup: false);

            await cleaner.FullCleanUp();

            managementService.Verify(service => service.GetQueueNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.GetExchangeNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.DeleteQueueAsync("Qs:app:queue", It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.DeleteExchangeAsync("Qs:app:exchange", It.IsAny<CancellationToken>()), Times.Once);
            managementService.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task FullCleanUp_WhenDangerousCleanupEnabled_DeletesAllNonReservedEntities()
        {
            var managementService = new Mock<IRqManagementService>(MockBehavior.Strict);
            managementService
                .Setup(service => service.GetQueueNamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "Qs:app:queue", "external.queue" });
            managementService
                .Setup(service => service.GetExchangeNamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "Qs:app:exchange", "external.exchange", "amq.direct" });
            managementService
                .Setup(service => service.DeleteQueueAsync("Qs:app:queue", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            managementService
                .Setup(service => service.DeleteQueueAsync("external.queue", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            managementService
                .Setup(service => service.DeleteExchangeAsync("Qs:app:exchange", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            managementService
                .Setup(service => service.DeleteExchangeAsync("external.exchange", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var cleaner = CreateCleaner(managementService.Object, allowDangerousFullCleanup: true);

            await cleaner.FullCleanUp();

            managementService.Verify(service => service.GetQueueNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.GetExchangeNamesAsync(It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.DeleteQueueAsync("Qs:app:queue", It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.DeleteQueueAsync("external.queue", It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.DeleteExchangeAsync("Qs:app:exchange", It.IsAny<CancellationToken>()), Times.Once);
            managementService.Verify(service => service.DeleteExchangeAsync("external.exchange", It.IsAny<CancellationToken>()), Times.Once);
            managementService.VerifyNoOtherCalls();
        }

        private static RqTransportFullCleaner CreateCleaner(
            IRqManagementService managementService,
            bool allowDangerousFullCleanup)
        {
            var configuration = new Configuration
            {
                AllowDangerousFullCleanup = allowDangerousFullCleanup
            };

            return new RqTransportFullCleaner(
                Mock.Of<ILogger<RqTransportFullCleaner>>(),
                configuration,
                managementService);
        }
    }
}
