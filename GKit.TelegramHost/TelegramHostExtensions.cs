using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GKit.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GKit.TelegramHost
{
    public interface ITelegramHostBuilder
    {
        ITelegramHostBuilder AddControllersWithRoutes();
        ITelegramHostBuilder AddRequestHandler<T>(Func<IServiceProvider, T>? factory) where T: class, IRequestHandler;
    }
    internal class TelegramHostBuilderImpl : ITelegramHostBuilder
    {
        private readonly IServiceCollection _services;
        public TelegramHostBuilderImpl(IServiceCollection services)
        {
            _services = services;
        }

        ITelegramHostBuilder ITelegramHostBuilder.AddControllersWithRoutes()
        {
            _services.AddScoped<IRequestHandler, ControllerRouteRequestHandler>();


            foreach(var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(p => p.GetTypes())
                .Where(p => p.IsAssignableTo(typeof(TelegramControllerBase)) && !p.IsAbstract))
            {
                _services.AddScoped(type);
            }

            return this;
        }

        ITelegramHostBuilder ITelegramHostBuilder.AddRequestHandler<T>(Func<IServiceProvider, T>? factory = null)
        {
            if(factory != null)
                _services.AddScoped<IRequestHandler, T>(factory);
            else
                _services.AddScoped<IRequestHandler, T>();
                
            return this;
        }
    }

    public static class TelegramHostExtensions
    {
        public static ITelegramHostBuilder AddTelegramHost(this IServiceCollection services, Action<TelegramHostOptions> config = null)
        {
            if(config != null)
                services.PostConfigure<TelegramHostOptions>(config);

            services.AddSingleton<ITelegramHostBroker, TelegramHostBroker>();
            services.AddScoped<IDeadLetterRequestHandler, LogDeadLetterRequestHandler>();

            services.AddSingleton<SettingsManager<TelegramHostOptions>, DefaultSettingsManager<TelegramHostOptions>>();
            services.AddSingleton<TelegramVerificationCodeManager>();
            services.AddSingleton<TelegramConnectionFactory>();
            services.AddSingleton<TelegramContextProvider>();

            services.AddScoped<TelegramContext>(provider => provider.GetRequiredService<TelegramContextProvider>().CreateContext());

            services.AddHostedService<TelegramHostService>();

            return new TelegramHostBuilderImpl(services);
        }
    }
}