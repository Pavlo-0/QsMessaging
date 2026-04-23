using QsMessaging.Public;

namespace CleanupArrangeInstance01;

internal interface ITransportCleanupExecutor
{
    Task EnsureOpenAsync();

    Task CleanUpAsync();

    Task FullCleanUpAsync();
}

internal sealed class TransportCleanupExecutor(
    IQsMessagingConnectionManager connectionManager,
    IQsMessagingTransportCleaner cleaner,
    IQsMessagingTransportFullCleaner fullCleaner) : ITransportCleanupExecutor
{
    public Task EnsureOpenAsync()
    {
        return connectionManager.IsConnected()
            ? Task.CompletedTask
            : connectionManager.Open();
    }

    public async Task CleanUpAsync()
    {
        await connectionManager.Close();
        await cleaner.CleanUp();
    }

    public async Task FullCleanUpAsync()
    {
        await connectionManager.Close();
        await fullCleaner.FullCleanUp();
    }
}
