using GKit.Settings;
using Microsoft.Extensions.DependencyInjection;
using SmtpServer.Authentication;
using SmtpServer.Storage;

namespace GKit.SmtpHost
{
    public interface ISmtpHostBuilder
    {
        ISmtpHostBuilder WithIdentityStore<T>() where T: class, ISmtpIdentityStore;
        ISmtpHostBuilder AddControllersWithRoutes();
        ISmtpHostBuilder AddMessageHandler<T>(Func<IServiceProvider, T>? factory) where T: class, IMessageHandler;
    }

    internal class SmtpHostBuilderImpl : ISmtpHostBuilder
    {
        private readonly IServiceCollection _services;
        public SmtpHostBuilderImpl(IServiceCollection services)
        {
            _services = services;
        }

        ISmtpHostBuilder ISmtpHostBuilder.AddControllersWithRoutes()
        {
            var routes = SmtpRouteTable.FromAppDomain();

            // Discovered once at startup and shared, rather than re-scanned per message.
            _services.AddSingleton(routes);
            _services.AddScoped<IMessageHandler, ControllerRouteMessageHandler>();

            foreach (var type in routes.ControllerTypes)
            {
                _services.AddScoped(type);
            }

            return this;
        }

        ISmtpHostBuilder ISmtpHostBuilder.AddMessageHandler<T>(Func<IServiceProvider, T>? factory)
        {
            if(factory != null)
                _services.AddScoped<IMessageHandler, T>(factory);
            else
                _services.AddScoped<IMessageHandler, T>();
                
            return this;
        }

        ISmtpHostBuilder ISmtpHostBuilder.WithIdentityStore<T>()
        {
            _services.AddTransient<ISmtpIdentityStore, T>();
            return this;
        }
    }

    public static class SmtpHostExtensions
    {
        public static ISmtpHostBuilder AddSmtpHost(this IServiceCollection services, Action<SmtpHostOptions>? config = null)
        {
            services.AddSingleton<ISmtpHostBroker, SmtpHostBroker>();

            // Fail closed. WithIdentityStore<T>() appends after this and therefore wins, because
            // GetRequiredService resolves the last registration for a service type.
            services.AddTransient<ISmtpIdentityStore, DenyAllSmtpIdentityStore>();
            services.AddTransient<IMessageStore, SmtpMessageStore>();
            services.AddTransient<IUserAuthenticator, SmtpAuthenticator>();

            services.AddScoped<IDeadLetterMessageHandler, FileDeadLetterMessageHandler>();

            if(config != null)
                services.PostConfigure<SmtpHostOptions>(config);

            services.AddSingleton<SmtpServerFactory>();
            services.AddSingleton<SettingsManager<SmtpHostOptions>, DefaultSettingsManager<SmtpHostOptions>>();

            services.AddHostedService<SmtpHostService>();

            return new SmtpHostBuilderImpl(services);
        }
    }
}