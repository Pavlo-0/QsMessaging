using QsMessaging.Shared.Models;

namespace QsMessaging.Shared.Services.Interfaces
{
    internal interface IConsumerService
    {
        Task UniversalConsumer(byte[] data, HandlersStoreRecord record, ConsumerMessageContext context, CancellationToken cancellationToken);
    }
}
