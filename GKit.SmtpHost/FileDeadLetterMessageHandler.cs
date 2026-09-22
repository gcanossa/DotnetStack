using Microsoft.Extensions.Options;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    /// <summary>
    /// Writes unhandled messages to disk as <c>.eml</c>.
    /// <para>
    /// Previously serialised only From/To/Subject/TextBody to JSON, discarding HTML bodies and
    /// every attachment — the one thing a dead letter exists for is being able to reprocess it.
    /// Filenames also collided: <c>DateTime.UtcNow.ToFileTimeUtc()</c> has 100 ns resolution, so
    /// two messages arriving together overwrote one another.
    /// </para>
    /// </summary>
    internal class FileDeadLetterMessageHandler(IOptions<SmtpHostOptions> options) : IDeadLetterMessageHandler
    {
        private readonly IOptions<SmtpHostOptions> _options = options;

        internal static string BuildFileName(DateTimeOffset timestamp, Guid discriminator) =>
            $"{timestamp:yyyyMMdd-HHmmss-fff}-{discriminator:N}.eml";

        public async Task Handle(IServiceProvider provider, MimeMessage message, ISessionContext context)
        {
            var path = _options.Value.DeadLettersPath;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var file = Path.Combine(path, BuildFileName(DateTimeOffset.UtcNow, Guid.NewGuid()));

            // The complete RFC 5322 message: headers, all bodies, all attachments.
            await message.WriteToAsync(file);
        }
    }
}
