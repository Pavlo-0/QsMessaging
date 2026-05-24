using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using AzureConnectionService = QsMessaging.AzureServiceBus.Services.Interfaces.IAsbConnectionService;
using RabbitConnectionService = QsMessaging.RabbitMq.Services.Interfaces.IRqConnectionService;

namespace QsMessagingUnitTests.Public
{
    [TestClass]
    public class QsMessagingRegisteringTest
    {
        private class CallingAssemblyContract { }

        private class CallingAssemblyHandler : IQsMessageHandler<CallingAssemblyContract>
        {
            public Task Consumer(CallingAssemblyContract contractModel) => Task.CompletedTask;
        }

        private sealed record DynamicHandlerAssembly(Assembly Assembly, Type HandlerInterfaceType, Type HandlerType);

        [TestInitialize]
        public void Setup()
        {
            ResetHandlerServiceState();
        }

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
        public void AddQsMessaging_WhenNoAssembliesConfigured_RegistersHandlersFromCallingAssembly()
        {
            var services = new ServiceCollection();

            services.AddQsMessaging(_ => { });

            Assert.IsTrue(services.Any(s =>
                s.ServiceType == typeof(IQsMessageHandler<CallingAssemblyContract>) &&
                s.ImplementationType == typeof(CallingAssemblyHandler)));
        }

        [TestMethod]
        public void AddQsMessaging_WhenAssembliesConfiguredInOptions_RegistersHandlersFromThoseAssemblies()
        {
            var dynamicHandlerAssembly = CreateDynamicMessageHandlerAssembly();
            var services = new ServiceCollection();

            services.AddQsMessaging(options =>
            {
                options.AssembliesToScan.Add(dynamicHandlerAssembly.Assembly);
            });

            Assert.IsTrue(services.Any(s =>
                s.ServiceType == dynamicHandlerAssembly.HandlerInterfaceType &&
                s.ImplementationType == dynamicHandlerAssembly.HandlerType));
        }

        [TestMethod]
        public void AddQsMessaging_WhenAssembliesPassedToOverload_RegistersHandlersFromThoseAssemblies()
        {
            var dynamicHandlerAssembly = CreateDynamicMessageHandlerAssembly();
            var services = new ServiceCollection();

            services.AddQsMessaging(_ => { }, dynamicHandlerAssembly.Assembly);

            Assert.IsTrue(services.Any(s =>
                s.ServiceType == dynamicHandlerAssembly.HandlerInterfaceType &&
                s.ImplementationType == dynamicHandlerAssembly.HandlerType));
        }

        [TestMethod]
        public void AddQsMessaging_WhenExplicitAssembliesHaveNoHandlers_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                services.AddQsMessaging(_ => { }, typeof(string).Assembly);
            });
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

        private static DynamicHandlerAssembly CreateDynamicMessageHandlerAssembly()
        {
            var assemblyName = new AssemblyName($"QsMessagingUnitTests.DynamicHandlers.{Guid.NewGuid():N}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");

            var contractType = moduleBuilder
                .DefineType($"{assemblyName.Name}.Contract", TypeAttributes.Public | TypeAttributes.Class)
                .CreateType();

            var handlerInterfaceType = typeof(IQsMessageHandler<>).MakeGenericType(contractType);
            var handlerTypeBuilder = moduleBuilder.DefineType(
                $"{assemblyName.Name}.Handler",
                TypeAttributes.Public | TypeAttributes.Class);
            handlerTypeBuilder.AddInterfaceImplementation(handlerInterfaceType);

            var consumerMethod = handlerTypeBuilder.DefineMethod(
                nameof(IQsMessageHandler<object>.Consumer),
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(Task),
                new[] { contractType });

            var il = consumerMethod.GetILGenerator();
            il.Emit(OpCodes.Call, typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetMethod!);
            il.Emit(OpCodes.Ret);

            handlerTypeBuilder.DefineMethodOverride(
                consumerMethod,
                handlerInterfaceType.GetMethod(nameof(IQsMessageHandler<object>.Consumer), new[] { contractType })!);

            var handlerType = handlerTypeBuilder.CreateType();

            return new DynamicHandlerAssembly(assemblyBuilder, handlerInterfaceType, handlerType);
        }

        private static void ResetHandlerServiceState()
        {
            var handlersField = typeof(HandlerService).GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Static);
            handlersField!.SetValue(null, new ConcurrentBag<HandlersStoreRecord>());

            var errorHandlersField = typeof(HandlerService).GetField("_consumerErrorHandler", BindingFlags.NonPublic | BindingFlags.Static);
            errorHandlersField!.SetValue(null, new ConcurrentBag<RqConsumerErrorHandlerStoreRecord>());
        }
    }
}
