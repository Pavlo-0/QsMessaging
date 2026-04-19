using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq;
using RabbitMQ.Client;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Reflection;

namespace QsMessagingUnitTests
{
    [TestClass]
    public class ConnectionServiceTest
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        //private Mock<QsMessagingConfiguration> _mockConfig;
        private Mock<IConnection> _mockConnection;
        private Mock<ILogger<RbConnectionService>> _mockLogger;
        private IRqConnectionService _connectionService;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        [TestInitialize]
        public void Setup()
        {
            ResetConnection();
            _mockConnection = new Mock<IConnection>();
            _mockLogger = new Mock<ILogger<RbConnectionService>>();
            var config = new Configuration();

            _connectionService = new RbConnectionService(_mockLogger.Object, config);
            _mockConnection.Setup(c => c.IsOpen).Returns(true);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResetConnection();
        }

        [TestMethod]
        public void GetConnection_ReturnsNull_WhenNoConnectionExists()
        {
            var result = _connectionService.GetConnection();
            Assert.IsNull(result);
        }
        /*
        [TestMethod]
        public async Task GetOrCreateConnectionAsync_ReturnsConnection_WhenConnectionIsCreated()
        {
            var result = await _connectionService.GetOrCreateConnectionAsync(CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsOpen);
        }*/

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_ReturnsExistingConnection_WhenConnectionIsAlreadyOpen()
        {
            GetConnectionField().SetValue(null, _mockConnection.Object);

            var result = await _connectionService.GetOrCreateConnectionAsync(CancellationToken.None);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsOpen);
        }

        private static void ResetConnection()
        {
            GetConnectionField().SetValue(null, null);
        }

        private static FieldInfo GetConnectionField()
        {
            return typeof(RbConnectionService).GetField("connection", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Unable to find RabbitMQ connection field.");
        }
    }
}
