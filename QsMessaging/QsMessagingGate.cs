using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging
{
    internal class QsMessagingGate(IRabbitMqSender rabbitMqSender) : IQsMessaging
    {
        public Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            ValidationType<TMessage>();
            ValidationModel(model);

            return rabbitMqSender.SendMessageAsync(model);
        }

        public Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            ValidationType<TEvent>();
            ValidationModel(model);

            return rabbitMqSender.SendEventAsync(model);
        }

        public Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken) where TRequest : class where TResponse : class
        {
            ValidationType<TRequest>("Request");
            ValidationType<TResponse>("Response");
            ValidationModel(request);

            return rabbitMqSender.SendRequest<TRequest, TResponse>(request, cancellationToken);    
        }

        private void ValidationModel<TModel>(TModel model, string field = "Model")
        {
            if (model == null) 
                throw new ArgumentNullException(nameof(model));
        }


        private void ValidationType<TModel>(string field = "Model" )
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
