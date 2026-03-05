using Autofac;
using Autofac.Extras.DynamicProxy;
using Castle.DynamicProxy;
using Toplanti.Core.Utilities.Interceptors;
using Toplanti.Core.Utilities.Security.JWT;
using Toplanti.Business.Abstract;
using Toplanti.Business.Concrete;
using Toplanti.DataAccess.Abstract;
using Toplanti.DataAccess.Concrete.EntityFramework;

namespace Toplanti.Business.DependencyResolvers.Autofac
{
    public class AutofacBusinessModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<UserManager>().As<IUserService>();
            builder.RegisterType<EfUserDal>().As<IUserDal>();
            builder.RegisterType<EfOperationClaimDal>().As<IOperationClaimDal>();
            builder.RegisterType<EfUserOperationClaimDal>().As<IUserOperationClaimDal>();

            builder.RegisterType<AuthenticationService>().As<IAuthenticationService>();
            builder.RegisterType<AuthTestManager>().As<IAuthTestService>();
            builder.RegisterType<ZoomProvisioningService>().As<IZoomProvisioningService>();
            builder.RegisterType<ZoomMeetingService>().As<IZoomMeetingService>();
            builder.RegisterType<JwtHelper>().As<ITokenHelper>();

            builder.RegisterType<LdapManager>().As<ILdapService>();
            builder.RegisterType<ZoomService>().As<IZoomService>();
            builder.RegisterType<AuthNotificationService>().As<IAuthNotificationService>();

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces()
                .EnableInterfaceInterceptors(new ProxyGenerationOptions()
                {
                    Selector = new AspectInterceptorSelector()
                }).InstancePerLifetimeScope();

        }
    }
}
