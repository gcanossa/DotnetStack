using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GKit.TelegramHost.Model;
using TL;

namespace GKit.TelegramHost;

public class TelegramConnection
{
    private readonly TelegramHostOptions _options;
    private readonly WTelegram.Client _client;
    private readonly ITelegramHostBroker _broker;
    private readonly TelegramVerificationCodeManager _verificationCodeManager;
    private readonly TelegramContextProvider _contextProvider;

    private readonly TaskCompletionSource _source = new TaskCompletionSource();

    public TelegramConnection(
        TelegramHostOptions options,
        TelegramVerificationCodeManager verificationCodeManager, 
        ITelegramHostBroker broker,
        TelegramContextProvider contextProvider)
    {
        _options = options;
        _client = new WTelegram.Client((what) =>
        {
            switch (what)
            {
                case "api_id": return options.AppId;
                case "api_hash": return options.AppHash;
                case "phone_number": return options.PhoneNumber;
                case "first_name": return options.FirstName;
                case "last_name": return options.LastName;
                case "session_pathname": return options.SessionFilePath;
                default: return null;
            }
        });
        _broker = broker;
        _verificationCodeManager = verificationCodeManager;

        _client.OnUpdates+=OnUpdate;

        _contextProvider = contextProvider;

        _contextProvider.UpdateClient(_client);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        if(_client.User is null)
        {
            string? value = _options.PhoneNumber;
            string key = "phone_number";
            while((key = await _client.Login(value)) is not null)
            {
                value = key switch {
                    "verification_code" => await _verificationCodeManager.RequestVerificationCode(),
                    _ => null
                };
            }
        }

        _contextProvider.ClientReady();
        
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while(await timer.WaitForNextTickAsync(cancellationToken) && !_stopped && !_source.Task.IsCompleted)
        {
            if(cancellationToken.IsCancellationRequested)
            {
                _source.SetCanceled(cancellationToken);
                break;
            }
        }

        await _source.Task;
    }

    private async Task OnUpdate(UpdatesBase updates)
    {
        foreach(var update in updates.UpdateList)
        {
            await _broker.EnqueueAsync(update, default(CancellationToken));
        }
    }

    protected bool _stopped = false;

    public void Stop()
    {
        if(!_stopped)
        {
            _client.Dispose();
            _contextProvider.InvalidateClient();
            _client.OnUpdates-=OnUpdate;
            _source.SetResult();

            _stopped = true;
        }
    }
}