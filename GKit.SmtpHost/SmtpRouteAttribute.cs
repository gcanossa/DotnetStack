using System.Text.RegularExpressions;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SmtpRouteAttribute(
        string? identity = null,
        string fromPattern = ".*",
        string toPattern = ".*",
        string subjectPattern = ".*")
        : Attribute
    {
        public string? Identity { get; init; } = identity;
        public string FromPattern { get; init; } = fromPattern;
        public string ToPattern { get; init; } = toPattern;
        public string SubjectPattern { get; init; } = subjectPattern;

        // Compiled once per attribute instance, with a timeout: subjects are attacker-supplied,
        // and a pathological pattern would otherwise hang the message pump.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

        private Regex? _from;
        private Regex? _to;
        private Regex? _subject;

        private Regex From => _from ??= Build(FromPattern);
        private Regex To => _to ??= Build(ToPattern);
        private Regex Subject => _subject ??= Build(SubjectPattern);

        private static Regex Build(string pattern) =>
            new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                MatchTimeout);

        /// <summary>
        /// Addresses are matched on the address itself, with the display name as a fallback.
        /// Matching <c>InternetAddress.Name</c> — the display name, usually empty — meant a
        /// pattern like <c>erp@.*</c> never matched anything.
        /// </summary>
        internal static bool MatchesAny(InternetAddressList addresses, Regex pattern)
        {
            if (addresses is null || addresses.Count == 0) return false;

            foreach (var address in addresses)
            {
                var candidate = address is MailboxAddress mailbox ? mailbox.Address : address.Name;

                if (!string.IsNullOrEmpty(candidate) && pattern.IsMatch(candidate)) return true;

                if (!string.IsNullOrEmpty(address.Name) && pattern.IsMatch(address.Name)) return true;
            }

            return false;
        }

        internal bool IsMatch(MimeMessage message, ISessionContext? context)
        {
            var user = context?.Authentication?.User;

            return (string.IsNullOrEmpty(Identity) || Identity == user) &&
                MatchesAny(message.From, From) &&
                MatchesAny(message.To, To) &&
                // Subject is nullable on a MimeMessage; Regex.IsMatch(null) threw.
                Subject.IsMatch(message.Subject ?? string.Empty);
        }
    }
}
