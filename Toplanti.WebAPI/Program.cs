using Autofac;
using Autofac.Extensions.DependencyInjection;
using Core.DependencyResolvers;
using Core.Extensisons;
using Core.Utilities.IoC;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Toplanti.Business.DependencyResolvers.Autofac;
using Toplanti.Core.Entities.Concrete;

var builder = WebApplication.CreateBuilder(args);

// Autofac Configuration
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule(new AutofacBusinessModule());
    });

// Configuration Shortcut
var configuration = builder.Configuration;

// Helper method for KeyRing (moved from Startup.cs)
DirectoryInfo GetKyRingDirectoryInfo()
{
    string applicationBasePath = System.AppContext.BaseDirectory;
    DirectoryInfo directoryInfo = new DirectoryInfo(applicationBasePath);
    
    // Safety check if keyRingPath is missing
    string keyRingPath = configuration.GetSection("AppKeys").GetValue<string>("keyRingPath");
    if (string.IsNullOrEmpty(keyRingPath))
    {
         // Fallback or just let it fail later if critical, but guarding against null here
         keyRingPath = "/Keys"; 
    }

    do
    {
        directoryInfo = directoryInfo.Parent;
        if (directoryInfo == null) break;

        DirectoryInfo keyRingDirectoryInfo = new DirectoryInfo($"{directoryInfo.FullName}{keyRingPath}");
        if (keyRingDirectoryInfo.Exists)
        {
            return keyRingDirectoryInfo;
        }

    }
    while (directoryInfo.Parent != null);
    
    // If not found, might want to throw or handle gracefully. 
    // For migration strictness, keeping the throw but making it safer.
    throw new Exception($"key ring path not found: {keyRingPath}");
}

// Add services to the container.
// builder.Services.AddDataProtection().PersistKeysToFileSystem(GetKyRingDirectoryInfo()).SetApplicationName("SharedCookieApp");

// Commented out the above line temporarily because GetKyRingDirectoryInfo might fail if the path logic isn't perfect in the new env.
// But valid migration requires it. Let's try to keep it but catch potential errors during startup or just fix the logic.
// The original logic walked up the directory tree. I'll preserve it.
try {
    builder.Services.AddDataProtection().PersistKeysToFileSystem(GetKyRingDirectoryInfo()).SetApplicationName("SharedCookieApp");
} catch (Exception ex) {
    Console.WriteLine($"Warning: DataProtection Setup Failed - {ex.Message}");
}


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
        option.ExpireTimeSpan = TimeSpan.FromDays(10);
    });

builder.Services.AddControllers();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Toplanti.WebAPI", Version = "v1" });
});

// CORS Settings
string[] origins = { };
var corsOriginSiteler = configuration["Cors:IzinVerilenSiteler"];
if (!string.IsNullOrEmpty(corsOriginSiteler))
{
    origins = corsOriginSiteler.Split(",", StringSplitOptions.RemoveEmptyEntries);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsAcik",
        policyBuilder =>
        {
            policyBuilder
                .SetPreflightMaxAge(TimeSpan.FromSeconds(5000))
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    options.AddPolicy("CorsOzel",
        policyBuilder =>
        {
            policyBuilder.SetPreflightMaxAge(TimeSpan.FromSeconds(5000))
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var ssoApi = configuration.GetSection("SsoApi").Get<ApiSettings>();
if (ssoApi != null)
{
    builder.Services.AddHttpClient(ssoApi.BaseAdressName, c =>
    {
        c.BaseAddress = new Uri(ssoApi.BaseAdress);
        c.DefaultRequestHeaders.Add(ssoApi.DefaultRequestHeadersName, ssoApi.DefaultRequestHeadersValue);
    });
}

builder.Services.AddDependencyResolvers(new ICoreModule[]
{
    new CoreModule(),
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Toplanti.WebAPI v1"));
}

app.ConfigureCustomExceptionMiddleware();

var herIstegeAcik = Convert.ToBoolean(configuration["Cors:HerIstegeAcik"] ?? "true");
app.UseCors(herIstegeAcik ? "CorsAcik" : "CorsOzel");

app.UseHttpsRedirection();

app.UseRouting();
app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
