using Microsoft.Extensions.Logging;

namespace GKit.TelegramHost;

public class TelegramConnectionFactory
{
    private readonly ILogger<TelegramConnectionFactory> _logger;
    private readonly ITelegramHostBroker _broker;
    private readonly TelegramVerificationCodeManager _verificationCodeManager;
    private readonly TelegramContextProvider _contextProvider;

    public TelegramConnectionFactory(
        ILogger<TelegramConnectionFactory> logger,
        ITelegramHostBroker broker,
        TelegramVerificationCodeManager verificationCodeManager,
        TelegramContextProvider contextProvider)
    {
        _logger = logger;
        _broker = broker;
        _verificationCodeManager = verificationCodeManager;
        _contextProvider = contextProvider;

        WTelegram.Helpers.Log = (level, message) => _logger.Log((LogLevel)level, message);
    }

    public TelegramConnection Create(TelegramHostOptions options)
    {
        return new TelegramConnection(options, _verificationCodeManager, _broker, _contextProvider);
    }
}