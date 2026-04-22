namespace AssertInstance01
{
    internal sealed class ScenarioExecutionGate : IScenarioExecutionGate
    {
        private readonly object _sync = new();
        private int _activeBlocks;
        private TaskCompletionSource<bool> _readyTcs = CreateCompletedTcs();

        public IAsyncDisposable BeginBlock()
        {
            lock (_sync)
            {
                if (_activeBlocks == 0)
                {
                    _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                _activeBlocks++;
            }

            return new BlockLease(this);
        }

        public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            Task waitTask;

            lock (_sync)
            {
                if (_activeBlocks == 0)
                {
                    return Task.CompletedTask;
                }

                waitTask = _readyTcs.Task;
            }

            return waitTask.WaitAsync(cancellationToken);
        }

        private void Release()
        {
            TaskCompletionSource<bool>? readyToSignal = null;

            lock (_sync)
            {
                if (_activeBlocks == 0)
                {
                    return;
                }

                _activeBlocks--;
                if (_activeBlocks == 0)
                {
                    readyToSignal = _readyTcs;
                }
            }

            readyToSignal?.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CreateCompletedTcs()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.TrySetResult(true);
            return tcs;
        }

        private sealed class BlockLease(ScenarioExecutionGate owner) : IAsyncDisposable, IDisposable
        {
            private readonly ScenarioExecutionGate _owner = owner;
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                _owner.Release();
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
