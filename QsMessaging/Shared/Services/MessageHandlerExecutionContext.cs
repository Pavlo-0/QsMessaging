namespace QsMessaging.Shared.Services
{
    internal static class MessageHandlerExecutionContext
    {
        internal sealed class HandlerState
        {
            public int Depth { get; set; }
            public Queue<Func<Task>> DeferredOperations { get; } = new();
            public object SyncRoot { get; } = new();
        }

        private static readonly AsyncLocal<HandlerState?> _state = new();

        public static bool IsInsideHandler => _state.Value?.Depth > 0;

        public static Scope Enter()
        {
            var state = _state.Value ??= new HandlerState();
            state.Depth++;
            return new Scope(state);
        }

        public static Task DeferUntilHandlerExitAsync(Func<Task> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            var state = _state.Value;
            if (state is null || state.Depth == 0)
            {
                return operation();
            }

            lock (state.SyncRoot)
            {
                state.DeferredOperations.Enqueue(operation);
            }

            return Task.CompletedTask;
        }

        private static async Task ExitAsync(HandlerState state)
        {
            Queue<Func<Task>> operationsToRun = new();

            lock (state.SyncRoot)
            {
                state.Depth = Math.Max(0, state.Depth - 1);
                if (state.Depth > 0)
                {
                    return;
                }

                while (state.DeferredOperations.Count > 0)
                {
                    operationsToRun.Enqueue(state.DeferredOperations.Dequeue());
                }

                _state.Value = null;
            }

            while (operationsToRun.Count > 0)
            {
                await operationsToRun.Dequeue()();
            }
        }

        internal sealed class Scope(HandlerState state) : IDisposable, IAsyncDisposable
        {
            private readonly HandlerState _state = state;
            private bool _disposed;

            public void Dispose()
            {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return;
                }

                await ExitAsync(_state);
                _disposed = true;
            }
        }
    }
}
