namespace QsMessaging.Shared.Interface
{
    internal interface IRRResponseHandler
    {
        Task Consumer(object contract, string correlationId);

        Task Consumer(object contract, string correlationId, CancellationToken cancellationToken = default)
        {
            return Consumer(contract, correlationId);
        }
    }
}
