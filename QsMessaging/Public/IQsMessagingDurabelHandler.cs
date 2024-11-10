namespace QsMessaging.Public
{
    public interface IQsMessagingDurabelHandler<TContract> where TContract : class
    {
        Task<bool> Consumer(Task contractModel);
    }
}
