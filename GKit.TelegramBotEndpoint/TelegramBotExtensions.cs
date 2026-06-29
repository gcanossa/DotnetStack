using GKit.Settings;
using GKit.TelegramBotEndpoint.Data;
using Microsoft.Extensions.DependencyInjection;

namespace GKit.TelegramBotEndpoint;

public static class TelegramBotExtensions
{
    public interface ITelegramBotBuilder
    {
        ITelegramBotBuilder WithSettingsManager<T, U>() 
            where T : SettingsManager<TelegramBotOptions<U>> 
            where U : IUpdateHandler;
        
        ITelegramBotBuilder WithDataProvider<T>() where T : class, ITelegramBotDataProvider;
    }

    internal class TelegramBotBuilderImpl<H>(IServiceCollection services) : ITelegramBotBuilder
        where H : IUpdateHandler
    {
        private Type? _dataProvider;
        private Type _settingsManager = typeof(DefaultSettingsManager<TelegramBotOptions<H>>);

        ITelegramBotBuilder ITelegramBotBuilder.WithDataProvider<T>()
        {
            _dataProvider = typeof(T);
            return this;
        }

        ITelegramBotBuilder ITelegramBotBuilder.WithSettingsManager<T, U>()
        {
            _settingsManager = typeof(T);
            return this;
        }

        public void Build()
        {
            services.AddSingleton(typeof(ITelegramBotDataProvider),
                _dataProvider ?? throw new ArgumentNullException(nameof(_dataProvider)));
            services.AddSingleton(typeof(SettingsManager<TelegramBotOptions<H>>), _settingsManager);
        }
    }

    public static IServiceCollection AddTelegramBot<T>(this IServiceCollection services,
        Action<ITelegramBotBuilder> config) where T : IUpdateHandler
    {
        services.AddSingleton<TelegramBotInfo<T>>();
        services.AddSingleton<TelegramBotClientAccessor<T>>();
        services.AddHostedService<TelegramBotService<T>>();

        var builder = new TelegramBotBuilderImpl<T>(services);
        config.Invoke(builder);
        builder.Build();

        return services;
    }
}