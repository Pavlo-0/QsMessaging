namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbTopicService
    {
        Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default);
    }
}
