namespace QsMessaging.Public.Handler
{
    public interface IQsMessageHandler<TModel> where TModel : class 
    {
        Task<bool> Consumer(TModel contractModel);
    }
}