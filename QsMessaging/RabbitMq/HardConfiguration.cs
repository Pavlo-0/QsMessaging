using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;

namespace QsMessaging.RabbitMq
{
    internal static class HardConfiguration
    {
        public struct SupportedInterfacesStruct
        {
            public Type TypeInterface { get; set; }
            public ExchangePurpose ExchangePurpose { get; init; }
            public QueueType Queue { get; init; }
            public ChannelPurpose ChannelPurpose { get; init; }
            public ConsumerType Consumer { get; set; }

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
                        Queue = QueueType.ConsumerTemporary,
                        ChannelPurpose = ChannelPurpose.QueueConsumerTemporary,
                        Consumer = ConsumerType.MessageEventConsumer
                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IQsMessageHandler<>),
                        ExchangePurpose = ExchangePurpose.Permanent,
                        Queue = QueueType.Permanent,
                        ChannelPurpose = ChannelPurpose.QueuePermanent,
                        Consumer = ConsumerType.MessageEventConsumer

                    },
                    new SupportedInterfacesStruct
                    {
                        TypeInterface = typeof(IRRResponseHandler),
                        ExchangePurpose = ExchangePurpose.Temporary,
                        Queue = QueueType.InstanceTemporary,
                        ChannelPurpose = ChannelPurpose.QueueInstanceTemporary,
                        Consumer = ConsumerType.RRResponseConsumer
                    },
                    new SupportedInterfacesStruct
                                        {
                        TypeInterface = typeof(IQsRequestResponseHandler<,>),
                        ExchangePurpose = ExchangePurpose.Temporary,
                        Queue = QueueType.SingleTemporary,
                        ChannelPurpose = ChannelPurpose.QueueSingleTemporary,
                        Consumer = ConsumerType.RRRequestConsumer
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

        public static ExchangePurpose GetExchangeByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ExchangePurpose;
        }

        public static QueueType GetQueueByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).Queue;
        }

        public static ChannelPurpose GetChannelPurposeByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).ChannelPurpose;
        }

        public static ConsumerType GetConsumerByInterfaceTypes(Type interfaceType)
        {
            return SupportedInterfaces.First(x => x.TypeInterface == interfaceType).Consumer;
        }
    }
}