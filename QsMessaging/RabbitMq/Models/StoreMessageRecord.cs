namespace QsMessaging.RabbitMq.Models
{
    internal record StoreMessageRecord(object RequestMessage, Type RequestMessageType, object? ResponseMessage, Type? ResponseMessageType, bool IsResponsed, DateTime CreateDate, TaskCompletionSource<bool> task);

}
