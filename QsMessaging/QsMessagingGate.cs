using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging
{
    internal class QsMessagingGate(ILogger<QsMessagingGate> logger, ISender sender) : IQsMessaging
    {
        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            ValidationType<TMessage>();
            ValidationModel(model);

            try
            {
                await sender.SendMessageAsync(model);
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation($"Operation was canceled: {oce.Message}");
            }
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            ValidationType<TEvent>();
            ValidationModel(model);

            try
            {
                await sender.SendEventAsync(model);
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation($"Operation was canceled: {oce.Message}");
            }

        }

        public async Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : class where TResponse : class
        {
            ValidationType<TRequest>("Request");
            ValidationType<TResponse>("Response");
            ValidationModel(request);

            if (typeof(TRequest) == typeof(TResponse))
                throw new NotSupportedException("The request type and response type must be different and cannot be the same.");

            try
            {
                return await sender.SendRequest<TRequest, TResponse>(request, cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                logger.LogInformation($"Operation was canceled: {oce.Message}");
                throw;
            }

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
