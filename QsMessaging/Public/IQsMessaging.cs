﻿namespace QsMessaging.Public
{
    public interface IQsMessaging
    {
        /// <summary>
        /// Send a message to another service. 
        /// On the receiving side, you must implement a handler that consumes the exact same message (by type). 
        /// If the message cannot be delivered immediately, it will wait until it can be successfully received.
        /// <see cref="IQsMessageHandler"/>
        /// If the handler receiver is an <see cref="IQsEventHandler"/>, the message can only be guaranteed to be delivered if the receiving application is active.
        /// </summary>
        /// <typeparam name="TMessage">The type of the message. This type will be used to determine which handler should be called to consume the message.</typeparam>
        /// <param name="Model"></param>
        Task SendMessageAsync<TMessage>(TMessage Model) where TMessage : class;

        /// <summary>
        /// Send a message to another service. 
        /// On the receiving side, you must implement a handler that consumes the exact same message (by type). 
        /// If the message cannot be delivered immediately, it will be discarded.
        /// <see cref="IQsEventHandler"/>
        /// </summary>
        /// <typeparam name="TEvent">The type of the message. This type will be used to determine which handler should be called to consume the message.</typeparam>
        /// <param name="Model"></param>
        Task SendEventAsync<TEvent>(TEvent Model) where TEvent : class;

        /// <summary>
        /// Send a request to another service and wait for the response
        /// On the receiving side, you must implement a handler that consumes the exact same Request (by type) and return the exact same Response.
        /// If a request cannot be delivered immediately, it will wait until a consumer is ready to process it. 
        /// If all possible consumers are disconnected, the request will be discarded.
        /// <see cref="IQsRequestResponseHandler<TRequest, TResponse>"/>
        /// </summary>
        /// <typeparam name="TRequest">The type of the request. This type will be used to determine which handler should be called to consume the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response. This type will be used to determine which handler should be called to consume the request.</typeparam>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request) where TRequest : class where TResponse : class;
    }
}
