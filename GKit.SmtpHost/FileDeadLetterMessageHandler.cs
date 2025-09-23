using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{    
    internal class FileDeadLetterMessageHandler : IDeadLetterMessageHandler
    {
        private readonly IOptions<SmtpHostOptions> _options;
        public FileDeadLetterMessageHandler(IOptions<SmtpHostOptions> options)
        {
            _options = options;
        }
        public async Task Handle(IServiceProvider provider, MimeMessage message, ISessionContext context)
        {
            if(!Directory.Exists(_options.Value.DeadLettersPath))
                Directory.CreateDirectory(_options.Value.DeadLettersPath);

            await File.WriteAllTextAsync(
                Path.Combine(_options.Value.DeadLettersPath, $"{DateTime.UtcNow.ToFileTimeUtc()}.json"), 
                JsonSerializer.Serialize(new {
                    From = message.From.ToString(), To = message.To.ToString(), message.Subject, message.TextBody
                })
            );
        }
    }
}