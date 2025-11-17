using GKit.Settings;
using GKit.TelegramBotEndpoint.Data;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.TelegramBotEndpoint;

public static class TelegramBotExtensions
{
    public interface ITelegramBotBuilder
    {
        ITelegramBotBuilder WithHandler<T>() where T : class, IUpdateHandler;
        ITelegramBotBuilder WithDataProvider<T>() where T : class, ITelegramBotDataProvider;
    }

    internal class TelegramBotBuilderImpl : ITelegramBotBuilder
    {
        private readonly IServiceCollection _services;
        public TelegramBotBuilderImpl(IServiceCollection services)
        {
            _services = services;
        }

        ITelegramBotBuilder ITelegramBotBuilder.WithDataProvider<T>()
        {
            _services.AddSingleton<ITelegramBotDataProvider, T>();
            return this;
        }

        ITelegramBotBuilder ITelegramBotBuilder.WithHandler<T>()
        {
            _services.AddScoped<IUpdateHandler, T>();
            return this;
        }
    }

    public static ITelegramBotBuilder AddTelegramBot(this IServiceCollection services, Action<TelegramBotOptions>? config = null)
    {
        if(config is not null)
        {
            services.PostConfigure(config);
        }

        services.AddSingleton<TelegramBotInfo>();
        services.AddSingleton<TelegramBotClientAccessor>();
        services.AddHostedService<TelegramBotService>();
        services.AddSingleton<SettingsManager<TelegramBotOptions>, DefaultSettingsManager<TelegramBotOptions>>();

        return new TelegramBotBuilderImpl(services);
    }
}