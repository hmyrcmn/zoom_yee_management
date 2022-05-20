using Core.DependencyResolvers;
using Core.Extensisons;
using Core.Utilities.IoC;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Toplanti.Core.Entities.Concrete;

namespace Toplanti.WebAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        private DirectoryInfo GetKyRingDirectoryInfo()
        {
            string applicationBasePath = System.AppContext.BaseDirectory;
            DirectoryInfo directoryInof = new DirectoryInfo(applicationBasePath);
            string keyRingPath = Configuration.GetSection("AppKeys").GetValue<string>("keyRingPath");
            do
            {
                directoryInof = directoryInof.Parent;

                DirectoryInfo keyRingDirectoryInfo = new DirectoryInfo($"{directoryInof.FullName}{keyRingPath}");
                if (keyRingDirectoryInfo.Exists)
                {
                    return keyRingDirectoryInfo;
                }

            }
            while (directoryInof.Parent != null);
            throw new Exception($"key ring path not foun");
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDataProtection().PersistKeysToFileSystem(GetKyRingDirectoryInfo()).SetApplicationName("SharedCookieApp");
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(option =>
                {
                    option.Cookie.Name = ".AspNet.SharedCookie";
                    option.Events.OnRedirectToLogin = (context) =>
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    };
                    option.Cookie.HttpOnly = true;
                    option.Cookie.IsEssential = true;
                    //option.Cookie.Domain = "yee.org.tr"; //canlý
                    option.Cookie.Domain = "localhost";  //local
                    option.ExpireTimeSpan = TimeSpan.FromDays(10);
                });

            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Toplanti.WebAPI", Version = "v1" });
            });

            //services.AddHttpClient();

            #region CORS_AYARLARI
            string[] origins = { };
            var corsOriginSiteler = Configuration["Cors:IzinVerilenSiteler"];
            if (!string.IsNullOrEmpty(corsOriginSiteler))
            {
                origins = corsOriginSiteler.Split(",", StringSplitOptions.RemoveEmptyEntries);
            }

            services.AddCors(options =>
            {
                options.AddPolicy("CorsAcik",

                    builder =>
                    {
                        builder
                            .SetPreflightMaxAge(TimeSpan.FromSeconds(5000))
                            .AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                        //  .AllowCredentials()
                        ;
                    });
                options.AddPolicy("CorsOzel",
                    builder =>
                    {
                        builder.SetPreflightMaxAge(TimeSpan.FromSeconds(5000))
                            .WithOrigins(origins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            //   .AllowCredentials()
                            // .Build();
                            ;
                    }
                );
            });
            #endregion CORS_AYARLARI

            //var ssoApi = Configuration.GetSection("SsoApi").Get<ApiSettings>();
            //services.AddHttpClient(ssoApi.BaseAdressName, c =>
            //{
            //    c.BaseAddress = new Uri(ssoApi.BaseAdress);
            //    c.DefaultRequestHeaders.Add(ssoApi.DefaultRequestHeadersName, ssoApi.DefaultRequestHeadersValue);
            //});


            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "dist"; // canlý da frontend klasörü olacak, development sýrasýnda önemli deðil bakmýyor zaten.
            });


            services.AddDependencyResolvers(new ICoreModule[]
            {
                new CoreModule(),
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Toplanti.WebAPI v1"));
            }

            app.ConfigureCustomExceptionMiddleware();

            //cors ayarlarýný okuyalým
            var herIstegeAcik = Convert.ToBoolean(Configuration["Cors:HerIstegeAcik"] ?? "true");
            app.UseCors(herIstegeAcik ? "CorsAcik" : "CorsOzel");

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSpa(spa =>
            {
                if (env.IsDevelopment()) // debug modda çalýþýr, publish edince çalýþmaz, localde vue yu çalýþtýrdýðýmýz adresi eklemek gerekir
                {
                    spa.UseProxyToSpaDevelopmentServer("http://localhost:8082");

                }
            });
        }
    }
}
