using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.SmtpHost
{
    public interface ISmtpIdentityStore
    {
        Task<bool> CheckAsync(string username, string password, CancellationToken cancellationToken);
    }
}