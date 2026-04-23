using QsMessaging.Shared.Models;

namespace QsMessaging.Shared.Services.Interfaces
{
    internal interface IConsumerService
    {
        Task UniversalConsumer(byte[] data, HandlersStoreRecord record, string? correlationId, string replyTo, string name, CancellationToken cancellationToken);
    }
}