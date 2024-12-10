using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;

namespace QsMessaging.RabbitMq
{
    internal static class HardConfiguration
    {
        public struct SupportedInterfacesStruct
        {
            public Type TypeInterface { get; set; }
            public ExchangePurpose ExchangePurpose { get; init; }
            public QueuePurpose QueuePurpose { get; init; }
            public ChannelPurpose ChannelPurpose { get; init; }
            public ConsumerPurpose ConsumerPurpose { get; set; }

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
                        ExchangePurpose = ExchangePurpose.Temporary,
                        QueuePurpose = QueuePurpose.ConsumerTemporary,
                        ChannelPurpose = ChannelPurpose.QueueConsumerTemporary,
                        ConsumerPurpose = ConsumerPurpose.MessageEventConsumer
                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsMessageHandler<>),
                        ExchangePurpose = ExchangePurpose.Permanent,
                        QueuePurpose = QueuePurpose.Permanent,
                        ChannelPurpose = ChannelPurpose.QueuePermanent,
                        ConsumerPurpose = ConsumerPurpose.MessageEventConsumer

                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IRRResponseHandler),
                        ExchangePurpose = ExchangePurpose.Temporary,
                        QueuePurpose = QueuePurpose.InstanceTemporary,
                        ChannelPurpose = ChannelPurpose.QueueInstanceTemporary,
                        ConsumerPurpose = ConsumerPurpose.RRResponseConsumer
                    },
                    new SupportedInterfacesStruct
                                        {
                        TypeInterface = typeof(IQsRequestResponseHandler<,>),
                        ExchangePurpose = ExchangePurpose.Temporary,
                        QueuePurpose = QueuePurpose.SingleTemporary,
                        ChannelPurpose = ChannelPurpose.QueueSingleTemporary,
                        ConsumerPurpose = ConsumerPurpose.RRRequestConsumer
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

        public static ExchangePurpose GetExchangePurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ExchangePurpose;
        }

        public static QueuePurpose GetQueuePurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).QueuePurpose;
        }

        public static ChannelPurpose GetChannelPurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ChannelPurpose;
        }

        public static ConsumerPurpose GetConsumerPurpose(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ConsumerPurpose;
        }
    }
}