using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq
{
    internal class RqConnectionManager(
        ILogger<RqConnectionManager> logger,
        IRqConnectionService connectionWorker,
        IRqChannelService channelService,
        ISubscriber subscriber) : IQsMessagingConnectionManager
    {
        private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);
        private readonly object _deferredLifecycleSync = new();
        private Task _deferredLifecycleTask = Task.CompletedTask;

        public async Task Close(CancellationToken cancellationToken = default)
        {
            if (MessageHandlerExecutionContext.IsInsideHandler)
            {
                logger.LogWarning("Scheduling RabbitMQ close after the message handler exits.");
                ScheduleLifecycleOperationAfterHandlerExit("close", () => CloseCoreAsync(cancellationToken));
                return;
            }

            await WaitForDeferredLifecycleCompletionAsync();
            await CloseCoreAsync(cancellationToken);
        }

        public bool IsConnected()
        {
            return IsConnected(connectionWorker.GetConnection());
        }

        public async Task Open()
        {
            if (MessageHandlerExecutionContext.IsInsideHandler)
            {
                logger.LogInformation("Scheduling RabbitMQ open after the message handler exits.");
                ScheduleLifecycleOperationAfterHandlerExit("open", () => OpenCoreAsync(CancellationToken.None));
                return;
            }

            await WaitForDeferredLifecycleCompletionAsync();
            await OpenCoreAsync(CancellationToken.None);
        }

        private void ScheduleLifecycleOperationAfterHandlerExit(
            string operationName,
            Func<Task> operation)
        {
            var handlerExitGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_deferredLifecycleSync)
            {
                var previousTask = _deferredLifecycleTask;
                _deferredLifecycleTask = previousTask.ContinueWith(
                    async previous =>
                    {
                        if (previous.IsFaulted)
                        {
                            logger.LogError(previous.Exception, "A deferred RabbitMQ lifecycle operation failed before {Operation}.", operationName);
                        }

                        await handlerExitGate.Task;
                        await operation();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();
            }

            _ = _deferredLifecycleTask.ContinueWith(
                task => logger.LogError(task.Exception, "Deferred RabbitMQ {Operation} operation failed.", operationName),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            _ = MessageHandlerExecutionContext.DeferUntilHandlerExitAsync(() =>
            {
                handlerExitGate.TrySetResult();
                return Task.CompletedTask;
            });
        }

        private bool IsConnected(IConnection? connection)
        {
            return connection is not null && connection.IsOpen;
        }

        private async Task CloseCoreAsync(CancellationToken cancellationToken)
        {
            await _lifecycleSemaphore.WaitAsync(cancellationToken);
            try
            {
                logger.LogInformation("Closing connection to RabbitMQ.");
                var connection = connectionWorker.GetConnection();
                if (connection is null)
                {
                    return;
                }

                await subscriber.CloseAsync(cancellationToken);
                await channelService.CloseByConnectionAsync(connection);
                await connectionWorker.CloseAsync(cancellationToken);
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
                logger.LogInformation("Opening connection to RabbitMQ.");
                await connectionWorker.GetOrCreateConnectionAsync(cancellationToken);
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
                logger.LogError(ex, "A deferred RabbitMQ lifecycle operation failed before the current request.");
            }
        }
    }
}
