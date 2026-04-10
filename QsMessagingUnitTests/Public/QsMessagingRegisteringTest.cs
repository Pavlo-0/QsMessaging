using Microsoft.Extensions.DependencyInjection;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Transporting.Interfaces;

namespace QsMessagingUnitTests.Public
{
    [TestClass]
    public class QsMessagingRegisteringTest
    {
        [TestMethod]
        public void AddQsMessaging_WhenAzureTransportHasNoConnectionString_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                services.AddQsMessaging(options =>
                {
                    options.Transport = QsMessagingTransport.AzureServiceBus;
                });
            });
        }

        [TestMethod]
        public void AddQsMessaging_WhenAzureTransportConfigured_RegistersAzureTransportServices()
        {
            var services = new ServiceCollection();

            services.AddQsMessaging(options =>
            {
                options.Transport = QsMessagingTransport.AzureServiceBus;
                options.AzureServiceBus.ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";
            });

            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(ITransportSender)));
            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(IClientService)));
            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(IAdministrationService)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingConnectionManager) &&
                s.ImplementationType == typeof(QsMessaging.AzureServiceBus.ConnectionManager)));
        }

        [TestMethod]
        public void AddQsMessaging_WhenRabbitMqTransportConfigured_RegistersRabbitMqTransportAdapter()
        {
            var services = new ServiceCollection();

            services.AddQsMessaging(_ => { });

            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(ITransportSender)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingConnectionManager) &&
                s.ImplementationType == typeof(QsMessaging.RabbitMq.ConnectionManager)));
        }
    }
}
