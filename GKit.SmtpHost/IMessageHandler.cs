using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    public interface IMessageHandler
    {
        Task<bool> Handle(IServiceProvider provider, MimeMessage message, ISessionContext context);
    }
}