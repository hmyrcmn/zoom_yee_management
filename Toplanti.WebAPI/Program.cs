using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Toplanti.Business.DependencyResolvers.Autofac;
using Toplanti.Core.DependencyResolvers;
using Toplanti.Core.Extensisons;
using Toplanti.Core.Utilities.IoC;
using Toplanti.Core.Utilities.Security.Encrytion;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.DataAccess.Concrete.EntityFramework.Contexts;
using Toplanti.WebAPI.Infrastructure;
using Toplanti.WebAPI.Middleware;
using Toplanti.WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);
// builder.WebHost.UseUrls("http://localhost:5001");

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule(new AutofacBusinessModule());
    });

var configuration = builder.Configuration;
var tokenOptions = configuration.GetSection("TokenOptions").Get<TokenOptions>()
    ?? throw new InvalidOperationException("TokenOptions section is required.");

if (string.IsNullOrWhiteSpace(tokenOptions.SecurityKey))
{
    throw new InvalidOperationException("TokenOptions:SecurityKey is required.");
}

var allowedOrigins = configuration.GetSection("Cors").GetValue<string>("IzinVerilenSiteler")?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .ToArray() ?? new[] { "http://localhost:8082" };

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddDbContext<ToplantiContext>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IZoomAdminAuditService, ZoomAdminAuditService>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Toplanti.WebAPI", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsAcik", policyBuilder =>
    {
        policyBuilder
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddDependencyResolvers(new ICoreModule[]
{
    new CoreModule(),
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ToplantiContext>();
    var connectionString = context.Database.GetConnectionString();
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        try
        {
            await DatabaseBootstrapper.BootstrapAsync(context, app.Environment.ContentRootPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Database bootstrap skipped - {ex.Message}");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Toplanti.WebAPI v1"));
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRouting();
app.UseCors("CorsAcik");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
