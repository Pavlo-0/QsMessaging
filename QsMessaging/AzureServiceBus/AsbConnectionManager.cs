using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Shared.Interface;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbConnectionManager(
        ILogger<AsbConnectionManager> logger,
        IAsbConnectionService connectionWorker,
        IAsbTopicSubscriptionService topicSubscriptionService,
        ISubscriber subscriber) : IQsMessagingConnectionManager
    {
        private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);
        private readonly object _deferredLifecycleSync = new();
        private Task _deferredLifecycleTask = Task.CompletedTask;

        public async Task Close(CancellationToken cancellationToken = default)
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                logger.LogInformation("Deferring Azure Service Bus close requested from inside a message handler.");
                await DeferCloseFromHandlerAsync();
                return;
            }

            await WaitForDeferredLifecycleCompletionAsync();
            await CloseCoreAsync(cancellationToken);
        }

        public bool IsConnected()
        {
            return connectionWorker.GetConnection() is { IsClosed: false };
        }

        public async Task Open()
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                logger.LogInformation("Deferring Azure Service Bus open requested from inside a message handler.");
                DeferLifecycleOperation("open", () => OpenCoreAsync(CancellationToken.None));
                return;
            }

            await WaitForDeferredLifecycleCompletionAsync();
            await OpenCoreAsync(CancellationToken.None);
        }

        private async Task DeferCloseFromHandlerAsync()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DeferLifecycleOperation(
                "close",
                async () =>
                {
                    started.TrySetResult();
                    await CloseCoreAsync(CancellationToken.None);
                });

            await started.Task;
        }

        private void DeferLifecycleOperation(
            string operationName,
            Func<Task> operation)
        {
            lock (_deferredLifecycleSync)
            {
                var previousTask = _deferredLifecycleTask;
                _deferredLifecycleTask = previousTask.ContinueWith(
                    async previous =>
                    {
                        if (previous.IsFaulted)
                        {
                            logger.LogError(previous.Exception, "A deferred Azure Service Bus lifecycle operation failed before {Operation}.", operationName);
                        }

                        await operation();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();
            }

            _ = _deferredLifecycleTask.ContinueWith(
                task => logger.LogError(task.Exception, "Deferred Azure Service Bus {Operation} operation failed.", operationName),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private async Task CloseCoreAsync(CancellationToken cancellationToken)
        {
            await _lifecycleSemaphore.WaitAsync(cancellationToken);
            try
            {
                logger.LogInformation("Closing Azure Service Bus transport.");

                try
                {
                    await subscriber.CloseAsync(cancellationToken);
                }
                finally
                {
                    try
                    {
                        await topicSubscriptionService.DeleteTemporarySubscriptionsAsync(cancellationToken);
                    }
                    finally
                    {
                        await connectionWorker.CloseAsync(cancellationToken);
                        await connectionWorker.CloseAdministrationClientAsync(cancellationToken);
                    }
                }

            }
            finally
            {
                _lifecycleSemaphore.Release();
            }
        }

        private async Task OpenCoreAsync(CancellationToken cancellationToken)
        {
            await _lifecycleSemaphore.WaitAsync(cancellationToken);
            try
            {
                logger.LogInformation("Opening Azure Service Bus transport.");
                await subscriber.SubscribeAsync(cancellationToken);
            }
            finally
            {
                _lifecycleSemaphore.Release();
            }
        }

        private async Task WaitForDeferredLifecycleCompletionAsync()
        {
            Task deferredTask;

            lock (_deferredLifecycleSync)
            {
                deferredTask = _deferredLifecycleTask;
            }

            try
            {
                await deferredTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A deferred Azure Service Bus lifecycle operation failed before the current request.");
            }
        }
    }
}
