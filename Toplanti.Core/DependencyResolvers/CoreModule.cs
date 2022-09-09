using Core.Utilities.IoC;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DependencyResolvers
{
    public class CoreModule : ICoreModule
    {
        public void Load(IServiceCollection serviceCollection)
        {
            serviceCollection.AddMemoryCache();
            serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            //Sso Api
            //serviceCollection.AddHttpClient("SsoApi", c =>
            //{
            //    c.BaseAddress = new Uri("https://sso.yee.org.tr");//canlı
            //    //c.BaseAddress = new Uri("http://localhost:5011/");//local
            //    c.DefaultRequestHeaders.Add("Accept", "application/json");
            //});
        }
    }
}
