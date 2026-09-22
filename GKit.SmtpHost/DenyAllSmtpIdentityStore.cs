using Microsoft.Extensions.Logging;

namespace GKit.SmtpHost
{
    /// <summary>
    /// The fail-closed default identity store: rejects every credential and explains how to
    /// configure a real one.
    /// <para>
    /// <see cref="SmtpHostExtensions.AddSmtpHost"/> registers this so that a host which forgets
    /// to call <c>WithIdentityStore&lt;T&gt;()</c> refuses mail rather than silently accepting
    /// it from anyone.
    /// </para>
    /// </summary>
    internal class DenyAllSmtpIdentityStore(ILogger<DenyAllSmtpIdentityStore> logger) : ISmtpIdentityStore
    {
        public Task<bool> CheckAsync(string username, string password, CancellationToken cancellationToken)
        {
            logger.LogError(
                "Rejecting SMTP authentication for {User}: no ISmtpIdentityStore is configured. " +
                "Call WithIdentityStore<T>() on the builder returned by AddSmtpHost, or use " +
                "WithIdentityStore<ConfigurationSmtpIdentityStore>() and populate SmtpHost:Users.",
                username);

            return Task.FromResult(false);
        }
    }
}
