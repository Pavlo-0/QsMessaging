using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;

namespace QsMessagingUnitTests.AzureServiceBus.Services
{
    [TestClass]
    public class ConnectionStringHelperTest
    {
        [TestMethod]
        public void GetAdministrationConnectionString_WhenNotUsingEmulator_ReturnsOriginalConnectionString()
        {
            var configuration = new QsAzureServiceBusConfiguration
            {
                ConnectionString = "Endpoint=sb://contoso.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=value;"
            };

            var result = ConnectionStringHelper.GetAdministrationConnectionString(configuration);

            Assert.AreEqual(configuration.ConnectionString, result);
        }

        [TestMethod]
        public void GetClientConnectionString_WhenEmulatorConnectionHasNoPort_AppendsAmqpPort()
        {
            var configuration = new QsAzureServiceBusConfiguration
            {
                ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
            };

            var result = ConnectionStringHelper.GetClientConnectionString(configuration);

            StringAssert.Contains(result, "Endpoint=sb://localhost:5672");
        }

        [TestMethod]
        public void GetClientConnectionString_WhenCustomAmqpPortIsConfigured_AppendsConfiguredPort()
        {
            var configuration = new QsAzureServiceBusConfiguration
            {
                ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                EmulatorAmqpPort = 5673
            };

            var result = ConnectionStringHelper.GetClientConnectionString(configuration);

            StringAssert.Contains(result, "Endpoint=sb://localhost:5673");
        }

        [TestMethod]
        public void GetAdministrationConnectionString_WhenEmulatorConnectionHasNoPort_AppendsManagementPort()
        {
            var configuration = new QsAzureServiceBusConfiguration
            {
                ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
            };

            var result = ConnectionStringHelper.GetAdministrationConnectionString(configuration);

            StringAssert.Contains(result, "Endpoint=sb://localhost:5300");
        }

        [TestMethod]
        public void GetAdministrationConnectionString_WhenMainEmulatorConnectionUsesExplicitAmqpPort_RewritesToManagementPort()
        {
            var configuration = new QsAzureServiceBusConfiguration
            {
                ConnectionString = "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
            };

            var result = ConnectionStringHelper.GetAdministrationConnectionString(configuration);

            StringAssert.Contains(result, "Endpoint=sb://localhost:5300");
        }

        [TestMethod]
        public void GetAdministrationConnectionString_WhenAdministrationConnectionStringIsProvided_PrefersExplicitValue()
        {
            var configuration = new QsAzureServiceBusConfiguration
            {
                ConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
                AdministrationConnectionString = "Endpoint=sb://localhost:7777;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
            };

            var result = ConnectionStringHelper.GetAdministrationConnectionString(configuration);

            Assert.AreEqual(configuration.AdministrationConnectionString, result);
        }
    }
}
