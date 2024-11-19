namespace QsMessaging.Public
{
    public interface IQsMessagingConnectionManager
    {
        Task Close();
        Task Open();
        bool IsConnected();
    }
}
