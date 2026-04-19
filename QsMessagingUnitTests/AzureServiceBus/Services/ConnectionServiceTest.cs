using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.RabbitMq;
using System.Reflection;

namespace QsMessagingUnitTests.AzureServiceBus.Services
{
    [TestClass]
    public class ConnectionServiceTest
    {
#pragma warning disable CS8618
        private Mock<ILogger<AsbConnectionService>> _mockLogger;
        private IAsbConnectionService _connectionService;
#pragma warning restore CS8618

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AsbConnectionService>>();
            _connectionService = new AsbConnectionService(_mockLogger.Object, CreateConfiguration());

            ResetConnectionAsync().GetAwaiter().GetResult();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResetConnectionAsync().GetAwaiter().GetResult();
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
            var client = CreateConnection();
            SetConnection(client);

            var result = _connectionService.GetConnection();

            Assert.AreSame(client, result);
        }

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_WhenConnectionAlreadyExists_ReturnsExistingConnection()
        {
            var client = CreateConnection();
            SetConnection(client);

            var result = await _connectionService.GetOrCreateConnectionAsync(CancellationToken.None);

            Assert.AreSame(client, result);
        }

        [TestMethod]
        public async Task CloseAsync_WhenConnectionExists_DisposesAndClearsConnection()
        {
            var client = CreateConnection();
            SetConnection(client);

            await _connectionService.CloseAsync(CancellationToken.None);

            Assert.IsNull(_connectionService.GetConnection());
            Assert.IsTrue(client.IsClosed);
        }

        [TestMethod]
        public async Task GetOrCreateConnectionAsync_WhenCancellationAlreadyRequested_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => _connectionService.GetOrCreateConnectionAsync(cts.Token));
        }

        private static Configuration CreateConfiguration()
        {
            return new Configuration
            {
                Transport = QsMessagingTransport.AzureServiceBus,
                AzureServiceBus = new QsAzureServiceBusConfiguration
                {
                    ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
                }
            };
        }

        private static ServiceBusClient CreateConnection()
        {
            return new ServiceBusClient(
                ConnectionStringHelper.GetClientConnectionString(CreateConfiguration().AzureServiceBus));
        }

        private static void SetConnection(ServiceBusClient? client)
        {
            GetConnectionField().SetValue(null, client);
        }

        private static async Task ResetConnectionAsync()
        {
            var field = GetConnectionField();
            if (field.GetValue(null) is ServiceBusClient client)
            {
                field.SetValue(null, null);
                await client.DisposeAsync();
                return;
            }

            field.SetValue(null, null);
        }

        private static FieldInfo GetConnectionField()
        {
            return typeof(AsbConnectionService).GetField("connection", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Unable to find Azure Service Bus connection field.");
        }
    }
}
