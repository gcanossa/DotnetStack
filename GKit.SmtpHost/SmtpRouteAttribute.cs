using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        internal bool IsMatch(MimeMessage message, ISessionContext context)
        {
            return (string.IsNullOrEmpty(Identity) || Identity == context.Authentication.User) &&
                message.From.Any(p => Regex.IsMatch(p.Name, FromPattern)) &&
                message.To.Any(p => Regex.IsMatch(p.Name, ToPattern)) &&
                Regex.IsMatch(message.Subject, SubjectPattern);
        }
    }
}