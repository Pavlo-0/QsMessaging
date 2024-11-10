namespace QsMessaging.Services.Interfaces
{
    internal interface IExchangeNameGenerator
    {
        string GetNameFromType<TModel>();
    }

    internal class ExchangeNameGenerator : IExchangeNameGenerator
    {
        public string GetNameFromType<TModel>()
        {
            //Add prefix to the string to make it more unique

            return "ex_" + typeof(TModel).FullName;
        }
    }

}
