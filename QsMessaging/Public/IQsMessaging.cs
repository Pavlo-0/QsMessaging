namespace QsMessaging.Public
{
    public interface IQsMessaging
    {
        Task<bool> SendMessageAsync<TMessage>(TMessage Model);
        Task<bool> SendEventAsync<TEvent>(TEvent Model);
    }
}
