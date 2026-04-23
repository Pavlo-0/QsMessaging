namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqManagementService
    {
        Task<IReadOnlyCollection<string>> GetQueueNamesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<string>> GetExchangeNamesAsync(CancellationToken cancellationToken = default);

        Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default);

        Task DeleteExchangeAsync(string exchangeName, CancellationToken cancellationToken = default);
    }
}
