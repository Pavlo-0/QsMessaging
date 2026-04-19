using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq;
using RabbitMQ.Client;
using System.Reflection;
using QsMessaging.RabbitMq.Services.Interfaces;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class ConnectionServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<RbConnectionService>> _mockLogger;
        private Mock<IConnection> _mockConnection;
        private IRqConnectionService _connectionService;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<RbConnectionService>>();
            _mockConnection = new Mock<IConnection>();

            _connectionService = new RbConnectionService(_mockLogger.Object, new Configuration());

            // Reset static connection field between tests
            var field = typeof(RbConnectionService).GetField("connection", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, null);
        }

        [TestMethod]
        public void GetConnection_WhenNoConnectionExists_ReturnsNull()
        {
            var result = _connectionService.GetConnection();

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetConnection_WhenConnectionExists_ReturnsConnection()
        {
            var field = typeof(RbConnectionService).GetField("connection", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, _mockConnection.Object);

            var result = _connectionService.GetConnection();

            Assert.AreEqual(_mockConnection.Object, result);
        }

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_WhenConnectionAlreadyOpen_ReturnsExistingConnection()
        {
            _mockConnection.Setup(c => c.IsOpen).Returns(true);

            var field = typeof(RbConnectionService).GetField("connection", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, _mockConnection.Object);

            var result = await _connectionService.GetOrCreateConnectionAsync(CancellationToken.None);

            Assert.AreEqual(_mockConnection.Object, result);
        }

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_WhenCancellationAlreadyRequested_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => _connectionService.GetOrCreateConnectionAsync(cts.Token));
        }
    }
}
