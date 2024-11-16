namespace QsMessaging.Public.Handler
{
    public interface IQsEventHandler<TModel> where TModel : class
    {
        Task<bool> Consumer(TModel message);
    }
}