using Microsoft.Extensions.DependencyInjection;
namespace QsMessaging
{
    internal class LazyService<T> : Lazy<T> where T : class
    {
        public LazyService(IServiceProvider serviceProvider)
            : base(() => serviceProvider.GetRequiredService<T>()) { }
    }
}
