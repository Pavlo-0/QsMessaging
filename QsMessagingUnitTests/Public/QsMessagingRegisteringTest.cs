using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interfaces;
using AzureConnectionService = QsMessaging.AzureServiceBus.Services.Interfaces.IAsbConnectionService;
using RabbitConnectionService = QsMessaging.RabbitMq.Services.Interfaces.IRqConnectionService;

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

            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(AzureConnectionService)));
            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(IAsbTopicService)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingConnectionManager) &&
                s.ImplementationType == typeof(AsbConnectionManager)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingTransportCleaner) &&
                s.ImplementationType == typeof(AsbTransportCleaner)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingTransportFullCleaner) &&
                s.ImplementationType == typeof(AsbTransportFullCleaner)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(AzureConnectionService) &&
                s.ImplementationType == typeof(AsbConnectionService)));
        }

        [TestMethod]
        public void AddQsMessaging_WhenRabbitMqTransportConfigured_RegistersRabbitMqTransportAdapter()
        {
            var services = new ServiceCollection();

            services.AddQsMessaging(_ => { });

            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(ISender)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(RabbitConnectionService) &&
                s.ImplementationType == typeof(QsMessaging.RabbitMq.Services.RbConnectionService)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingConnectionManager) &&
                s.ImplementationType == typeof(RqConnectionManager)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingTransportCleaner) &&
                s.ImplementationType == typeof(RqTransportCleaner)));
            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessagingTransportFullCleaner) &&
                s.ImplementationType == typeof(RqTransportFullCleaner)));
        }

        [TestMethod]
        public async Task CleanUpTransportation_ClosesCurrentTransportBeforeRunningCleaner()
        {
            var manager = new Mock<IQsMessagingConnectionManager>();
            var cleaner = new Mock<IQsMessagingTransportCleaner>();
            var serviceProvider = new Mock<IServiceProvider>();
            var host = new Mock<IHost>();
            var sequence = new MockSequence();

            manager.InSequence(sequence)
                .Setup(m => m.Close(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            cleaner.InSequence(sequence)
                .Setup(c => c.CleanUp(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            serviceProvider
                .Setup(sp => sp.GetService(typeof(IQsMessagingConnectionManager)))
                .Returns(manager.Object);
            serviceProvider
                .Setup(sp => sp.GetService(typeof(IQsMessagingTransportCleaner)))
                .Returns(cleaner.Object);
            host.SetupGet(h => h.Services).Returns(serviceProvider.Object);

            var returnedHost = await host.Object.CleanUpTransportation();

            Assert.AreSame(host.Object, returnedHost);
            manager.Verify(m => m.Close(It.IsAny<CancellationToken>()), Times.Once);
            cleaner.Verify(c => c.CleanUp(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task FullCleanUpTransportation_ClosesCurrentTransportBeforeRunningFullCleaner()
        {
            var manager = new Mock<IQsMessagingConnectionManager>();
            var cleaner = new Mock<IQsMessagingTransportFullCleaner>();
            var serviceProvider = new Mock<IServiceProvider>();
            var host = new Mock<IHost>();
            var sequence = new MockSequence();

            manager.InSequence(sequence)
                .Setup(m => m.Close(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            cleaner.InSequence(sequence)
                .Setup(c => c.FullCleanUp(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            serviceProvider
                .Setup(sp => sp.GetService(typeof(IQsMessagingConnectionManager)))
                .Returns(manager.Object);
            serviceProvider
                .Setup(sp => sp.GetService(typeof(IQsMessagingTransportFullCleaner)))
                .Returns(cleaner.Object);
            host.SetupGet(h => h.Services).Returns(serviceProvider.Object);

            var returnedHost = await host.Object.FullCleanUpTransportation();

            Assert.AreSame(host.Object, returnedHost);
            manager.Verify(m => m.Close(It.IsAny<CancellationToken>()), Times.Once);
            cleaner.Verify(c => c.FullCleanUp(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
