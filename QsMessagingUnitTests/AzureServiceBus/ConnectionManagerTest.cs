using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Shared.Interface;
using AzureConnectionService = QsMessaging.AzureServiceBus.Services.Interfaces.IConnectionService;

namespace QsMessagingUnitTests.AzureServiceBus
{
    [TestClass]
    public class ConnectionManagerTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<AsbConnectionManager>> _mockLogger;
        private Mock<AzureConnectionService> _mockConnectionService;
        private Mock<IAdministrationService> _mockAdministrationService;
        private Mock<ISubscriber> _mockSubscriber;
        private AsbConnectionManager _connectionManager;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbConnectionManager>>();
            _mockConnectionService = new Mock<AzureConnectionService>();
            _mockAdministrationService = new Mock<IAdministrationService>();
            _mockSubscriber = new Mock<ISubscriber>();

            _connectionManager = new AsbConnectionManager(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockAdministrationService.Object,
                _mockSubscriber.Object);
        }

        [TestMethod]
        public async Task Close_WhenDeleteOwnedEntitiesThrows_StillDisposesConnection()
        {
            _mockSubscriber
                .Setup(s => s.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockAdministrationService
                .Setup(s => s.DeleteOwnedEntitiesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("cleanup failed"));
            _mockConnectionService
                .Setup(s => s.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _connectionManager.Close());

            _mockConnectionService.Verify(s => s.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Open_WhenCloseIsInProgress_WaitsForCloseToFinish()
        {
            var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowCloseToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeFinished = false;

            _mockSubscriber
                .Setup(s => s.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    closeStarted.TrySetResult();
                    await allowCloseToFinish.Task;
                    closeFinished = true;
                });
            _mockAdministrationService
                .Setup(s => s.DeleteOwnedEntitiesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockConnectionService
                .Setup(s => s.GetConnection())
                .Returns((ServiceBusClient?)null);
            _mockSubscriber
                .Setup(s => s.SubscribeAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Assert.IsTrue(closeFinished);
                    return Task.CompletedTask;
                });

            var closeTask = _connectionManager.Close();
            await closeStarted.Task;

            var openTask = _connectionManager.Open();

            _mockSubscriber.Verify(s => s.SubscribeAsync(It.IsAny<CancellationToken>()), Times.Never);

            allowCloseToFinish.TrySetResult();

            await Task.WhenAll(closeTask, openTask);

            _mockSubscriber.Verify(s => s.SubscribeAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
