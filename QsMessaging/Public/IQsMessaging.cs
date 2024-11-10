namespace QsMessaging.Public
{
    public interface IQsMessaging
    {
        Task<bool> SendMessageAsync<T>(T Model);
        Task<bool> SendEventAsync<T>(T Model);

    }
}
