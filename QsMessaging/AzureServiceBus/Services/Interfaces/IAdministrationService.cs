namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAdministrationService
    {
        Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default);
    }
}
