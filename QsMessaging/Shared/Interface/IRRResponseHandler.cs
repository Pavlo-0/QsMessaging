namespace QsMessaging.Shared.Interface
{
    internal interface IRRResponseHandler
    {
        Task Consumer(object contract, string correlationId);
    }
}
