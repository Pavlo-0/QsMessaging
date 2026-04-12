namespace QsMessaging.AzureServiceBus
{
    internal static class AsbMessageHandlerExecutionContext
    {
        private static readonly AsyncLocal<int> _depth = new();

        public static bool IsInsideHandler => _depth.Value > 0;

        public static IDisposable Enter()
        {
            _depth.Value++;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _depth.Value = Math.Max(0, _depth.Value - 1);
                _disposed = true;
            }
        }
    }
}
