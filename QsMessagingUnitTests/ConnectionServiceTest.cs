
using Moq;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessagingUnitTests
{
    [TestClass]
    public class ConnectionServiceTest
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        //private Mock<QsMessagingConfiguration> _mockConfig;
        private Mock<IConnection> _mockConnection;
        private IConnectionService _connectionService;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        [TestInitialize]
        public void Setup()
        {
            _mockConnection = new Mock<IConnection>();

            var config = new QsMessagingConfiguration();

            _connectionService = new ConnectionService(config);
            _mockConnection.Setup(c => c.IsOpen).Returns(true);
        }

        [TestMethod]
        public void GetConnection_ReturnsNull_WhenNoConnectionExists()
        {
            var result = _connectionService.GetConnection();
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_ReturnsConnection_WhenConnectionIsCreated()
        {
            var result = await _connectionService.GetOrCreateConnectionAsync(CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsOpen);
        }

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_ReturnsExistingConnection_WhenConnectionIsAlreadyOpen()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            _connectionService.GetType().GetField("connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, _mockConnection.Object);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            var result = await _connectionService.GetOrCreateConnectionAsync(CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsOpen);
        }
    }
}
