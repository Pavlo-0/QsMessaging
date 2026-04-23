using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Services;
using QsMessaging.Shared.Interface;
using RabbitMQ.Client;
using QsMessaging.RabbitMq;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class ConnectionManagerTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RqConnectionManager>> _mockLogger;
        private Mock<IRqConnectionService> _mockConnectionService;
        private Mock<IRqChannelService> _mockChannelService;
        private Mock<ISubscriber> _mockSubscriber;
        private Mock<IConnection> _mockConnection;
        private IQsMessagingConnectionManager _connectionManager;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RqConnectionManager>>();
            _mockConnectionService = new Mock<IRqConnectionService>();
            _mockChannelService = new Mock<IRqChannelService>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockConnection = new Mock<IConnection>();

            _connectionManager = new RqConnectionManager(
                _mockLogger.Object,
                _mockConnectionService.Object,
                _mockChannelService.Object,
                _mockSubscriber.Object);
        }

        [TestMethod]
        public async Task Close_WhenConnectionIsNull_ReturnsImmediately()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns((IConnection?)null);

            await _connectionManager.Close();

            _mockConnectionService.Verify(s => s.CloseAsync(It.IsAny<CancellationToken>()), Times.Never);
            _mockChannelService.Verify(s => s.CloseByConnectionAsync(It.IsAny<IConnection>()), Times.Never);
        }

        [TestMethod]
        public async Task Close_WhenConnectionExists_ClosesChannelsAndConnection()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns(_mockConnection.Object);
            _mockChannelService
                .Setup(s => s.CloseByConnectionAsync(_mockConnection.Object))
                .Returns(Task.CompletedTask);
            _mockConnectionService
                .Setup(s => s.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _connectionManager.Close();

            _mockChannelService.Verify(s => s.CloseByConnectionAsync(_mockConnection.Object), Times.Once);
            _mockConnectionService.Verify(s => s.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Close_WhenCancellationRequested_ExitsLoop()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => _connectionManager.Close(cts.Token));
        }

        [TestMethod]
        public void IsConnected_WhenConnectionIsNull_ReturnsFalse()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns((IConnection?)null);

            var result = _connectionManager.IsConnected();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsConnected_WhenConnectionIsOpen_ReturnsTrue()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns(_mockConnection.Object);
            _mockConnection.Setup(c => c.IsOpen).Returns(true);

            var result = _connectionManager.IsConnected();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsConnected_WhenConnectionIsClosed_ReturnsFalse()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns(_mockConnection.Object);
            _mockConnection.Setup(c => c.IsOpen).Returns(false);

            var result = _connectionManager.IsConnected();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task Open_CallsSubscriberSubscribeAsync()
        {
            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockSubscriber.Setup(s => s.SubscribeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            await _connectionManager.Open();

            _mockConnectionService.Verify(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockSubscriber.Verify(s => s.SubscribeAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task CloseAndOpen_WhenCalledInsideMessageHandler_DefersLifecycleUntilHandlerExits()
        {
            var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowCloseToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscribeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _mockConnectionService
                .Setup(s => s.GetConnection())
                .Returns(_mockConnection.Object);
            _mockChannelService
                .Setup(s => s.CloseByConnectionAsync(_mockConnection.Object))
                .Returns(async () =>
                {
                    closeStarted.TrySetResult();
                    await allowCloseToFinish.Task;
                });
            _mockSubscriber
                .Setup(s => s.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockConnectionService
                .Setup(s => s.CloseAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockConnectionService
                .Setup(s => s.GetOrCreateConnectionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockConnection.Object);
            _mockSubscriber
                .Setup(s => s.SubscribeAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    subscribeStarted.TrySetResult();
                    return Task.CompletedTask;
                });

            await using (MessageHandlerExecutionContext.Enter())
            {
                var closeTask = _connectionManager.Close();
                var openTask = _connectionManager.Open();

                Assert.IsTrue(closeTask.IsCompletedSuccessfully);
                Assert.IsTrue(openTask.IsCompletedSuccessfully);
                _mockChannelService.Verify(s => s.CloseByConnectionAsync(It.IsAny<IConnection>()), Times.Never);
                _mockSubscriber.Verify(s => s.SubscribeAsync(It.IsAny<CancellationToken>()), Times.Never);
            }

            await closeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _mockSubscriber.Verify(s => s.SubscribeAsync(It.IsAny<CancellationToken>()), Times.Never);

            allowCloseToFinish.TrySetResult();
            await subscribeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            _mockChannelService.Verify(s => s.CloseByConnectionAsync(_mockConnection.Object), Times.Once);
            _mockSubscriber.Verify(s => s.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockConnectionService.Verify(s => s.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockSubscriber.Verify(s => s.SubscribeAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
