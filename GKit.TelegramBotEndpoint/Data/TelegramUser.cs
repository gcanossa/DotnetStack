namespace GKit.TelegramBotEndpoint.Data;

public enum TelegramUserStatus
{
    LockedOut,
    Authorizing,
    Joining,
    Joined
}

public class TelegramUser
{
    public long Id { get; set; }

    public TelegramUserStatus Status { get; set; }

    public int ErrorCount { get; set; } = 0;

    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public string? VerificationCode { get; set; }
    public DateTimeOffset? VerificationCodeExpiration { get; set; }
}