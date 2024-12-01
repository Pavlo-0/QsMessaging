namespace QsMessaging.Public.Handler
{
    internal interface IQsRequestResponseHandler<TRequest, TResponse>
    {
        Task<TResponse> Consumer(TRequest request);
    }
}
