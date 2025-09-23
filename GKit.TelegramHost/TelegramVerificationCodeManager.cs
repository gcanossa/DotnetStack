namespace GKit.TelegramHost;

public class TelegramVerificationCodeManager
{
    private readonly object _sync = new object();

    private TaskCompletionSource<string>? _pendingRequest = null;

    public bool IsVerificationCodeRequested { get; private set; }
    public event Action OnVerificationCodeRequest;
    public event Action<string> OnVerificationCodeResponse;

    public void RespondVerificationCode(string verificationCode)
    {
        lock(_sync){
            _pendingRequest?.SetResult(verificationCode);
        }

        OnVerificationCodeResponse?.Invoke(verificationCode);
    }

    public async Task<string> RequestVerificationCode()
    {
        try
        {
            lock(_sync){
                IsVerificationCodeRequested = true;
                _pendingRequest = _pendingRequest ?? new TaskCompletionSource<string>();
            }

            OnVerificationCodeRequest?.Invoke();

            return await _pendingRequest.Task;
        }
        finally
        {
            lock(_sync){
                _pendingRequest = null;
                IsVerificationCodeRequested = false;
            }
        }
    }
}