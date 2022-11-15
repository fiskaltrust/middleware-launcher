using fiskaltrust.Middleware.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
    public class MiddlewareBootstrapper<T> : IMiddlewareBootstrapper where T : class
    {
        private readonly T _instance;
        public MiddlewareBootstrapper(T instance)
        {
            _instance = instance;
        }

        public Guid Id { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(_instance);
        }
    }
}