using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GKit.SmtpHost
{
    /// <summary>
    /// Authenticates against the credentials in <see cref="SmtpHostOptions.Users"/>.
    /// <para>
    /// Opt in explicitly with <c>WithIdentityStore&lt;ConfigurationSmtpIdentityStore&gt;()</c>.
    /// It is deliberately not the default: this store previously returned <c>true</c> for every
    /// username and password, which made <c>AuthenticationRequired(true)</c> meaningless and
    /// left the server accepting mail from anyone.
    /// </para>
    /// </summary>
    public class ConfigurationSmtpIdentityStore(IOptions<SmtpHostOptions> options) : ISmtpIdentityStore
    {
        protected IOptions<SmtpHostOptions> _options = options;

        public Task<bool> CheckAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            var users = _options.Value.Users;

            if (users is null || users.Count == 0 || string.IsNullOrEmpty(username))
                return Task.FromResult(false);

            if (!users.TryGetValue(username, out var expected) || string.IsNullOrEmpty(expected))
                return Task.FromResult(false);

            // Fixed-time comparison so a wrong password cannot be recovered by timing the reply.
            var match = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(password ?? string.Empty));

            return Task.FromResult(match);
        }
    }
}
