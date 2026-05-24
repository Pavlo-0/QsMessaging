namespace QsMessaging.Shared.Models
{
    internal record StoreMessageRecord(object RequestMessage, Type RequestMessageType, object? ResponseMessage, Type? ResponseMessageType, bool IsResponded, DateTime CreateDate, TaskCompletionSource<bool> task);

}
