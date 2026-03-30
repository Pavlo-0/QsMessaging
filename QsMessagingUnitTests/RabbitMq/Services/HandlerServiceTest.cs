using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace QsMessagingUnitTests.RabbitMq.Services
{
    [TestClass]
    public class HandlerServiceTest
    {
#pragma warning disable CS8618
        private IHandlerService _handlerService;
#pragma warning restore CS8618

        private class TestContract { }

        private class TestMessageHandler : IQsMessageHandler<TestContract>
        {
            public Task Consumer(TestContract contractModel) => Task.CompletedTask;
        }

        [TestInitialize]
        public void Setup()
        {
            // Reset static bags between tests
            var handlersField = typeof(HandlerService).GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Static);
            handlersField!.SetValue(null, new ConcurrentBag<HandlersStoreRecord>());

            var errorHandlersField = typeof(HandlerService).GetField("_consumerErrorHandler", BindingFlags.NonPublic | BindingFlags.Static);
            errorHandlersField!.SetValue(null, new ConcurrentBag<ConsumerErrorHandlerStoreRecord>());

            // Use an assembly with no handlers to start clean
            _handlerService = new HandlerService(new ServiceCollection(), typeof(string).Assembly);
        }

        [TestMethod]
        public void GetHandlers_WhenAssemblyHasNoHandlers_ReturnsEmpty()
        {
            var result = _handlerService.GetHandlers();

            Assert.IsFalse(result.Any());
        }

        [TestMethod]
        public void GetHandlers_WhenAssemblyContainsHandlerImplementation_FindsHandler()
        {
            var service = new HandlerService(new ServiceCollection(), Assembly.GetExecutingAssembly());

            var result = service.GetHandlers(typeof(IQsMessageHandler<>)).ToList();

            Assert.IsTrue(result.Any(r => r.HandlerType == typeof(TestMessageHandler)));
        }

        [TestMethod]
        public void GetHandlers_WhenAssemblyContainsHandlerImplementation_RecordHasCorrectGenericType()
        {
            var service = new HandlerService(new ServiceCollection(), Assembly.GetExecutingAssembly());

            var result = service.GetHandlers(typeof(IQsMessageHandler<>)).ToList();

            var record = result.First(r => r.HandlerType == typeof(TestMessageHandler));
            Assert.AreEqual(typeof(TestContract), record.GenericType);
        }

        [TestMethod]
        public void AddRRResponseHandler_WhenCalled_ReturnsRecordWithCorrectGenericType()
        {
            var record = _handlerService.AddRRResponseHandler<TestContract>();

            Assert.IsNotNull(record);
            Assert.AreEqual(typeof(TestContract), record.GenericType);
        }

        [TestMethod]
        public void AddRRResponseHandler_WhenCalled_HandlerIsReturnedByGetHandlers()
        {
            _handlerService.AddRRResponseHandler<TestContract>();

            var result = _handlerService.GetHandlers(typeof(IRRResponseHandler)).ToList();

            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void GetHandlers_WithSpecificSupportedInterfaceType_ReturnsOnlyMatchingHandlers()
        {
            _handlerService.AddRRResponseHandler<TestContract>();

            var rrHandlers = _handlerService.GetHandlers(typeof(IRRResponseHandler)).ToList();
            var messageHandlers = _handlerService.GetHandlers(typeof(IQsMessageHandler<>)).ToList();

            Assert.AreEqual(1, rrHandlers.Count);
            Assert.IsFalse(messageHandlers.Any());
        }

        [TestMethod]
        public void RegisterAllHandlers_WhenCalled_RegistersRRResponseHandlerInServiceCollection()
        {
            var services = new ServiceCollection();
            var handlerService = new HandlerService(services, typeof(string).Assembly);

            handlerService.RegisterAllHandlers();

            Assert.IsTrue(services.Any(s => s.ServiceType == typeof(IRRResponseHandler)));
        }

        [TestMethod]
        public void GetConsumerErrorHandlers_WhenNoErrorHandlersRegistered_ReturnsEmpty()
        {
            var result = _handlerService.GetConsumerErrorHandlers();

            Assert.IsFalse(result.Any());
        }
    }
}
