using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared.Interface;

namespace QsMessaging.Shared
{
    internal static class HardConfiguration
    {
        public struct SupportedInterfacesStruct
        {
            public Type TypeInterface { get; set; }
            public RqExchangePurpose ExchangePurpose { get; init; }
            public RqQueuePurpose QueuePurpose { get; init; }
            public RqChannelPurpose ChannelPurpose { get; init; }
            public RqConsumerPurpose ConsumerPurpose { get; set; }
            public AsbReciverPurpose ReciverPurpose { get; set; }

            public AbsSubscriptionPurpose SubscriptionPurpose { get; set; }

            public string Name
            {
                get
                {
                    return TypeInterface.FullName ?? throw new Exception("Error with HardConfiguration");
                }
            }
        }

        public static SupportedInterfacesStruct[] SupportedInterfaces
        {
            get
            {
                return new SupportedInterfacesStruct[]
                {
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsEventHandler<>),
                        ExchangePurpose = RqExchangePurpose.Temporary,
                        QueuePurpose = RqQueuePurpose.ConsumerTemporary,
                        ChannelPurpose = RqChannelPurpose.QueueConsumerTemporary,
                        ConsumerPurpose = RqConsumerPurpose.MessageEventConsumer,
                        ReciverPurpose = AsbReciverPurpose.TopicSubscription,
                        SubscriptionPurpose = AbsSubscriptionPurpose.Temporary
                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsMessageHandler<>),
                        ExchangePurpose = RqExchangePurpose.Permanent,
                        QueuePurpose = RqQueuePurpose.Permanent,
                        ChannelPurpose = RqChannelPurpose.QueuePermanent,
                        ConsumerPurpose = RqConsumerPurpose.MessageEventConsumer,
                        ReciverPurpose = AsbReciverPurpose.TopicSubscription,
                        SubscriptionPurpose = AbsSubscriptionPurpose.Permanent

                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IRRResponseHandler),
                        ExchangePurpose = RqExchangePurpose.Temporary,
                        QueuePurpose = RqQueuePurpose.InstanceTemporary,
                        ChannelPurpose = RqChannelPurpose.QueueInstanceTemporary,
                        ConsumerPurpose = RqConsumerPurpose.RRResponseConsumer,
                        ReciverPurpose = AsbReciverPurpose.QueueForResponse,
                    },
                    new SupportedInterfacesStruct
                                        {
                        TypeInterface = typeof(IQsRequestResponseHandler<,>),
                        ExchangePurpose = RqExchangePurpose.Temporary,
                        QueuePurpose = RqQueuePurpose.SingleTemporary,
                        ChannelPurpose = RqChannelPurpose.QueueSingleTemporary,
                        ConsumerPurpose = RqConsumerPurpose.RRRequestConsumer,
                        ReciverPurpose = AsbReciverPurpose.QueueForRequest
                    },
                };
            }
        }

        public static Type[] SupportedInterfacesTypes
        {
            get
            {
                return SupportedInterfaces.Select(x => x.TypeInterface).ToArray();
            }
        }

        public static SupportedInterfacesStruct GetConfigurationByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType);
        }

        public static RqExchangePurpose GetExchangePurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ExchangePurpose;
        }

        public static RqQueuePurpose GetQueuePurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).QueuePurpose;
        }

        public static RqChannelPurpose GetChannelPurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ChannelPurpose;
        }

        public static RqConsumerPurpose GetConsumerPurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ConsumerPurpose;
        }

        public static AsbReciverPurpose GetReciverPurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ReciverPurpose;
        }

        public static AbsSubscriptionPurpose GetSubscriptionPurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).SubscriptionPurpose;
        }
    }
}