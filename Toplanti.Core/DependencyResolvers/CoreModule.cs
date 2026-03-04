using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Toplanti.Core.Utilities.IoC;

namespace Toplanti.Core.DependencyResolvers
{
    public class CoreModule : ICoreModule
    {
        public void Load(IServiceCollection serviceCollection)
        {
            serviceCollection.AddMemoryCache();
            serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }
    }
}
