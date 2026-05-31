using QsMessaging.Public.Handler;
using QsMessaging.Shared.Models;

namespace QsMessaging.Shared.Services.Interfaces
{
    internal interface IFailedMessageQueuePublisher
    {
        string GetErrorQueueName(ConsumerMessageContext context);

        Task SendAsync(FailedMessageWrapper wrapper, CancellationToken cancellationToken);
    }
}
