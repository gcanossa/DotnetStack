using GKit.Settings;
using GKit.TelegramBotEndpoint.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using GKit.TelegramBotEndpoint.Data;

namespace GKit.TelegramBotEndpoint;

public class TelegramBotService : BackgroundService
{
    private TelegramBotClientAccessor _clientAccessor;
    private readonly SettingsManager<TelegramBotOptions> _optionsManager;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly TelegramBotInfo _botInfo;
    private readonly IServiceProvider _provider;

    private TaskCompletionSource _opCs;
    private CancellationTokenSource _mainCts;
    private CancellationTokenSource _opCts;

    public TelegramBotService(
        TelegramBotClientAccessor clientAccessor,
        SettingsManager<TelegramBotOptions> optionsManager, 
        ILogger<TelegramBotService> logger, 
        TelegramBotInfo botInfo,
        IServiceProvider provider)
    {
        _clientAccessor = clientAccessor;
        _provider = provider;
        _logger = logger;

        _opCs = new TaskCompletionSource();

        _botInfo = botInfo;

        _optionsManager = optionsManager;
        _optionsManager.OptionsChanged += OnOptionsChanged;
    }

    public override void Dispose()
    {
        _optionsManager.OptionsChanged -= OnOptionsChanged;

        base.Dispose();
    }

    private async Task OnOptionsChanged(TelegramBotOptions options)
    {
        await StopClientAsync();
    }

    protected async Task StopClientAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        if(_clientAccessor.Client is not null)
        {
            _opCts?.Cancel();
            _clientAccessor.Client = null;
            _opCs.SetResult();
            _opCs = null;
        }
    }

    protected async Task StartClientAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        if(_clientAccessor.Client is {})
        {
            throw new InvalidOperationException("Previous client must be stopped firtst.");
        }

        _opCs = new TaskCompletionSource();
        _opCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        ReceiverOptions receiverOptions = new ()
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        var options = await _optionsManager.GetOptionsAsync();
        _clientAccessor.Client = new TelegramBotClient(options.BotToken);

        var me = await _clientAccessor.Client.GetMeAsync(_opCts.Token);
        _logger.LogInformation("Bot started for {UserId}: {UserUsername}", me.Id, me.Username);

        _botInfo.Name = me.Username;

        _clientAccessor.Client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _opCts.Token
        );

        await _opCs.Task;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _mainCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        while(!_mainCts.Token.IsCancellationRequested)
        {
            await StartClientAsync(_mainCts.Token);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _mainCts?.Cancel();
        await StopClientAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }

    private async Task<bool> ErrorAndCheckLockout(TelegramUser user, TelegramBotOptions options, ITelegramBotDataProvider dataProvider)
    {
        user.ErrorCount++;
                
        if(user.ErrorCount >= options.LockoutErrorCount)
        {
            user.Status = TelegramUserStatus.LockedOut;
            _logger.LogWarning("User {UserId} locked out after {ErrorCount} errors", user.Id, user.ErrorCount);
            
            return true;
        }

        await dataProvider.UpdateUser(user);

        return false;
    }


    protected async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        using var scope = _provider.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var _dataProvider = provider.GetRequiredService<ITelegramBotDataProvider>();

        var options = await _optionsManager.GetOptionsAsync();

        var userId = 
            update.Message is {} ? update.Message.From?.Id :
            update.CallbackQuery is {} ? update.CallbackQuery.From?.Id :
            null;

        var chatId =
            update.Message is {} ? update.Message.Chat.Id :
            update.CallbackQuery is {} ? update.CallbackQuery.Message?.Chat?.Id :
            null;

        if(userId is null)
        {
            _logger.LogWarning("Unhandled update type {UpdateType}", update.Type);
            return;
        }
        
        var user = await _dataProvider.GetUserById(userId.Value);

        if(user is null && !options.AllowRegistration)
        {
            _logger.LogWarning($"Unknown user update skipped because: {nameof(TelegramBotOptions.AllowRegistration)}=false");
            return;
        }

        if(user is null && update.Message?.Text != "/start")
        {
            _logger.LogWarning("Unknown user update skipped because: Not a /start command.");
            return;
        }

        if(user is null)
        {
            await _dataProvider.CreateUser(new TelegramUser(){
                Id = userId.Value,
                Status = TelegramUserStatus.Joining
            });

            await botClient.SendTextMessageAsync(update.Message!.Chat.Id, "Condividi le tue informazioni di contatto", 
                replyMarkup: new ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Invia")));

            _logger.LogInformation("Registration started for {UserId}", userId.Value);
            return;
        }

        switch(user.Status)
        {
            case TelegramUserStatus.Joining: 
                if(update.Message?.Contact is null)
                {                
                    _logger.LogWarning("Registration for {UserId}: missing contact info", userId.Value);
                    if(!await ErrorAndCheckLockout(user, options, _dataProvider))
                    {
                        await botClient.SendTextMessageAsync(update.Message!.Chat.Id, "Condividi le tue informazioni di contatto", 
                            replyMarkup: new ReplyKeyboardMarkup(KeyboardButton.WithRequestContact("Invia")));
                    }
                }
                else
                {
                    user.ErrorCount = 0;

                    user.Status = TelegramUserStatus.Authorizing;

                    user.PhoneNumber = update.Message.Contact.PhoneNumber;
                    user.FirstName = update.Message.Contact.FirstName;
                    user.LastName = update.Message.Contact.LastName;

                    await botClient.SendTextMessageAsync(update.Message!.Chat.Id, "Inserisci il codice di verifica:",
                        replyMarkup: new ReplyKeyboardRemove()
                    );

                    await _dataProvider.UpdateUser(user);
                    _logger.LogInformation("Registration for {UserId}: Started authorization", userId.Value);
                }
                break;
            case TelegramUserStatus.Authorizing:
                if(update.Message?.Text is null)
                {
                    _logger.LogWarning("Registration for {UserId}: missing verification code text", userId.Value);
                    if(!await ErrorAndCheckLockout(user, options, _dataProvider))
                    {
                        await botClient.SendTextMessageAsync(update.Message!.Chat.Id, "Inserisci il codice di verifica:");
                    }
                }
                else
                {
                    if(update.Message.Text != user.VerificationCode || user.VerificationCodeExpiration < DateTimeOffset.Now)
                    {
                        _logger.LogWarning("Registration for {UserId}: wrong or expired verification code", userId.Value);
                        if(!await ErrorAndCheckLockout(user, options, _dataProvider))
                        {
                            await botClient.SendTextMessageAsync(update.Message!.Chat.Id, "Codice errato o scaduto. Riprova:");
                        }
                    }
                    else
                    {
                        user.ErrorCount = 0;

                        _logger.LogInformation("Registration for {UserId}: completed", userId.Value);
                        user.Status = TelegramUserStatus.Joined;

                        user.VerificationCode = null;
                        user.VerificationCodeExpiration = null;

                        await botClient.SendTextMessageAsync(update.Message!.Chat.Id, $"Registrazione completata {user.FirstName}! 🚀");

                        await _dataProvider.UpdateUser(user);
                    }
                }
                break;
        }

        if(user.Status is not TelegramUserStatus.Joined)
        {
            return;
        }
        
        var handler = provider.GetRequiredService<IUpdateHandler>();
        await handler.HandleUpdateAsync(botClient, update, user, chatId);
    }

    protected Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        if(exception is ApiRequestException apiRequestException)
        {
            _logger.LogError("Telegram API Error: {ErrorCode} {Message}", 
                apiRequestException.ErrorCode, apiRequestException.Message);
        }
        else
        {
            _logger.LogError("Error: {Message}", exception.Message);
        }

        return Task.CompletedTask;
    }
}