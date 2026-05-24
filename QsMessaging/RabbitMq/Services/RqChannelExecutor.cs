using RabbitMQ.Client;
using System.Runtime.CompilerServices;

namespace QsMessaging.RabbitMq.Services
{
    internal static class RqChannelExecutor
    {
        private static readonly ConditionalWeakTable<IChannel, SemaphoreSlim> ChannelLocks = new();

        public static async Task ExecuteAsync(
            IChannel channel,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(channel);
            ArgumentNullException.ThrowIfNull(action);

            var semaphore = ChannelLocks.GetValue(channel, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                await action(cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static async Task<TResult> ExecuteAsync<TResult>(
            IChannel channel,
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(channel);
            ArgumentNullException.ThrowIfNull(action);

            var semaphore = ChannelLocks.GetValue(channel, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                return await action(cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
