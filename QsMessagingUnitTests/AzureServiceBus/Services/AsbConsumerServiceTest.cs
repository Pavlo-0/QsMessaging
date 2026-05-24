using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using QsMessaging.AzureServiceBus;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace QsMessagingUnitTests.AzureServiceBus.Services
{
    [TestClass]
    public class AsbConsumerServiceTest
    {
        private sealed class TestMessage
        {
            public string Name { get; set; } = string.Empty;
        }

        [TestMethod]
        public async Task HandleMessageAsync_DelegatesToUniversalConsumerForSharedHandlerRetry()
        {
            const string entityName = "topic/subscription";
            const string correlationId = "corr-1";
            const string replyTo = "reply-queue";
            var payload = JsonSerializer.Serialize(new TestMessage { Name = "from-asb" });
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(payload),
                correlationId: correlationId,
                replyTo: replyTo);
            var args = new ProcessMessageEventArgs(
                message,
                Mock.Of<ServiceBusReceiver>(),
                new CancellationToken(canceled: true));
            var record = new HandlersStoreRecord(
                typeof(IQsMessageHandler<>),
                typeof(IQsMessageHandler<TestMessage>),
                typeof(IQsMessageHandler<TestMessage>),
                typeof(TestMessage));
            var consumerService = new Mock<IConsumerService>();
            var serviceProvider = new ServiceCollection().BuildServiceProvider();
            var asbConsumerService = new AsbConsumerService(
                Mock.Of<ILogger<AsbSubscriber>>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                consumerService.Object,
                Mock.Of<ISender>());

            await asbConsumerService.HandleMessageAsync(args, record, entityName, CancellationToken.None);

            consumerService.Verify(
                x => x.UniversalConsumer(
                    It.Is<byte[]>(bytes => Encoding.UTF8.GetString(bytes) == payload),
                    record,
                    correlationId,
                    replyTo,
                    entityName,
                    It.Is<CancellationToken>(token => token.CanBeCanceled)),
                Times.Once);
        }
    }
}
