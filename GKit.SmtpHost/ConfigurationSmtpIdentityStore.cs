using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace GKit.SmtpHost
{
    public class ConfigurationSmtpIdentityStore(IOptions<SmtpHostOptions> options) : ISmtpIdentityStore
    {
        protected IOptions<SmtpHostOptions> _options = options;

        public Task<bool> CheckAsync(string username, string password, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = true;
            
            return Task.FromResult(result);
        }
    }
}