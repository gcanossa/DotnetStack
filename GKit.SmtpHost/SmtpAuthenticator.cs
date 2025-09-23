using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtpServer;
using SmtpServer.Authentication;

namespace GKit.SmtpHost
{
    public class SmtpAuthenticator : IUserAuthenticator
    {
        protected ISmtpIdentityStore _store;
        private ILogger<SmtpAuthenticator> _logger;

        public SmtpAuthenticator(ISmtpIdentityStore store, ILogger<SmtpAuthenticator> logger)
        {
            _store = store;
            _logger = logger;
        }
        public async Task<bool> AuthenticateAsync(ISessionContext context, string user, string password, CancellationToken cancellationToken)
        {
            var operationId = Guid.NewGuid();
            
            _logger.LogInformation("Authentication for user: {User}", user);
            
            var result = await _store.CheckAsync(user, password, cancellationToken);

            _logger.LogInformation("Result={Result}", result);

            return result;
        }
    }
}