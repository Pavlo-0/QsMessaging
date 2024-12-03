namespace QsMessaging.Public.Handler
{
    public interface IQsRequestResponseHandler<TRequest, TResponse>
    {
        Task<TResponse> Consumer(TRequest request);
    }
}
