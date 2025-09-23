using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    public interface ISmtpHostBroker
    {
        ValueTask EnqueueAsync(MimeMessage message, ISessionContext context, CancellationToken cancellationToken);
        
        ValueTask<(MimeMessage, ISessionContext)> DequeueAsync(CancellationToken cancellationToken);

        IAsyncEnumerable<(MimeMessage, ISessionContext)> ProcessAllAsync(CancellationToken cancellationToken);
    }
}