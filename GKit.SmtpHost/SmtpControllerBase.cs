using SmtpServer;

namespace GKit.SmtpHost
{
    public abstract class SmtpControllerBase
    {
        public ISessionContext? Context { get; internal set; }
    }

    public class SmtpSkippedActionException : Exception
    {
    }

    public class SmtpFailedActionException : Exception
    {
    }

    public enum SmtpControllerActionResult
    {
        Success,
        Failure,
        Skipped
    }
}