using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.Public;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessagingUnitTests.RabbitMq
{
    [TestClass]
    public class ConnectionManagerTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<ConnectionManager>> _mockLogger;
        private Mock<IConnectionService> _mockConnectionService;
        private Mock<ISubscriber> _mockSubscriber;
        private Mock<IConnection> _mockConnection;
        private IQsMessagingConnectionManager _connectionManager;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<ConnectionManager>>();
            _mockConnectionService = new Mock<IConnectionService>();
            _mockSubscriber = new Mock<ISubscriber>();
            _mockConnection = new Mock<IConnection>();

            _connectionManager = new ConnectionManager(_mockLogger.Object, _mockConnectionService.Object, _mockSubscriber.Object);
        }

        [TestMethod]
        public async Task Close_WhenConnectionIsNull_ReturnsImmediately()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns((IConnection?)null);

            await _connectionManager.Close();

            _mockConnection.As<IAsyncDisposable>().Verify(d => d.DisposeAsync(), Times.Never);
        }

        [TestMethod]
        public async Task Close_WhenConnectionExists_DisposesConnection()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns(_mockConnection.Object);
            _mockConnection.Setup(c => c.IsOpen).Returns(false);
            _mockConnection.As<IAsyncDisposable>().Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);

            await _connectionManager.Close();

            _mockConnection.As<IAsyncDisposable>().Verify(d => d.DisposeAsync(), Times.Once);
        }

        [TestMethod]
        public async Task Close_WhenCancellationRequested_ExitsLoop()
        {
            _mockConnectionService.Setup(s => s.GetConnection()).Returns(_mockConnection.Object);
            _mockConnection.Setup(c => c.IsOpen).Returns(true);
            _mockConnection.As<IAsyncDisposable>().Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Should not loop indefinitely
            await _connectionManager.Close(cts.Token);
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
            _mockSubscriber.Setup(s => s.SubscribeAsync()).Returns(Task.CompletedTask);

            await _connectionManager.Open();

            _mockSubscriber.Verify(s => s.SubscribeAsync(), Times.Once);
        }
    }
}
