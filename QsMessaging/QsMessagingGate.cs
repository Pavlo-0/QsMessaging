using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging
{
    internal class QsMessagingGate(ILogger<QsMessagingGate> logger, IRabbitMqSender rabbitMqSender) : IQsMessaging
    {
        public Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            ValidationType<TMessage>();
            ValidationModel(model);

            try
            {
                return rabbitMqSender.SendMessageAsync(model);
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation($"Operation was canceled: {oce.Message}");
            }

            return Task.CompletedTask;
        }

        public Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            ValidationType<TEvent>();
            ValidationModel(model);

            try
            {
                return rabbitMqSender.SendEventAsync(model);
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation($"Operation was canceled: {oce.Message}");
            }

            return Task.CompletedTask;

        }

        public Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : class where TResponse : class
        {
            ValidationType<TRequest>("Request");
            ValidationType<TResponse>("Response");
            ValidationModel(request);

            try
            {
                return rabbitMqSender.SendRequest<TRequest, TResponse>(request, cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation($"Operation was canceled: {oce.Message}");
            }

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            return Task.FromResult((TResponse?)null);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.

        }

        private void ValidationModel<TModel>(TModel model, string field = "Model")
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
        }


        private void ValidationType<TModel>(string field = "Model")
        {
            // Prevent usage of string
            if (typeof(TModel) == typeof(string))
                throw new NotSupportedException($"{field} cannot be of type string.");

            // Prevent usage of object
            if (typeof(TModel) == typeof(object))
                throw new NotSupportedException($"{field} cannot be of type object.");
        }
    }
}
