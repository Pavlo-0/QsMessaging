using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Shared.Interface;
using AzureConnectionService = QsMessaging.AzureServiceBus.Services.Interfaces.IAbsConnectionService;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbConnectionManager(
        ILogger<AsbConnectionManager> logger,
        AzureConnectionService connectionWorker,
        ISubscriber subscriber) : IQsMessagingConnectionManager
    {
        private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);
        private readonly object _queuedLifecycleSync = new();
        private Task _queuedLifecycleTask = Task.CompletedTask;

        public Task Close(CancellationToken cancellationToken = default)
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                logger.LogInformation("Deferring Azure Service Bus close requested from inside a message handler.");
                _ = QueueLifecycleOperation("close", CloseCoreAsync, CancellationToken.None, awaitCompletion: false);
                return Task.CompletedTask;
            }

            return QueueLifecycleOperation("close", CloseCoreAsync, cancellationToken, awaitCompletion: true);
        }

        public bool IsConnected()
        {
            return connectionWorker.GetConnection() is { IsClosed: false };
        }

        public Task Open()
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                logger.LogInformation("Deferring Azure Service Bus open requested from inside a message handler.");
                _ = QueueLifecycleOperation("open", _ => OpenCoreAsync(), CancellationToken.None, awaitCompletion: false);
                return Task.CompletedTask;
            }

            return QueueLifecycleOperation("open", _ => OpenCoreAsync(), CancellationToken.None, awaitCompletion: true);
        }

        private Task QueueLifecycleOperation(
            string operationName,
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken,
            bool awaitCompletion)
        {
            Task scheduledTask;

            lock (_queuedLifecycleSync)
            {
                var previousTask = _queuedLifecycleTask;
                scheduledTask = _queuedLifecycleTask = previousTask.ContinueWith(
                    async previous =>
                    {
                        if (previous.IsFaulted)
                        {
                            logger.LogError(previous.Exception, "A queued Azure Service Bus lifecycle operation failed before {Operation}.", operationName);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        await operation(cancellationToken);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();
            }

            if (!awaitCompletion)
            {
                _ = scheduledTask.ContinueWith(
                    task => logger.LogError(task.Exception, "Deferred Azure Service Bus {Operation} operation failed.", operationName),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);

                return Task.CompletedTask;
            }

            return scheduledTask;
        }

        private async Task CloseCoreAsync(CancellationToken cancellationToken)
        {
            await _lifecycleSemaphore.WaitAsync(cancellationToken);
            try
            {
                logger.LogInformation("Closing Azure Service Bus transport.");
                await subscriber.CloseAsync(cancellationToken);
                await connectionWorker.CloseAsync(cancellationToken);
                await connectionWorker.CloseAdministrationClientAsync(cancellationToken);
                try
                {
                    //await administrationService.DeleteOwnedEntitiesAsync(cancellationToken);
                }
                finally
                {
                }
            }
            finally
            {
                _lifecycleSemaphore.Release();
            }
        }

        private async Task OpenCoreAsync()
        {
            await _lifecycleSemaphore.WaitAsync();
            try
            {
                logger.LogInformation("Opening Azure Service Bus transport.");
                await subscriber.SubscribeAsync();
            }
            finally
            {
                _lifecycleSemaphore.Release();
            }
        }
    }
}
