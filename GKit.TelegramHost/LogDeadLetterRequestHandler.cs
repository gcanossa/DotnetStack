using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TL;

namespace GKit.TelegramHost
{
    internal class LogDeadLetterRequestHandler : IDeadLetterRequestHandler
    {
        private readonly ILogger<IDeadLetterRequestHandler> _logger;

        public LogDeadLetterRequestHandler(ILogger<IDeadLetterRequestHandler> logger)
        {
            _logger = logger;
        }
        public Task Handle(IServiceProvider provider, IObject request)
        {
            var jsonOptions = new JsonSerializerOptions() { IncludeFields = true };
            _logger.LogInformation("Unbale to handle request of {Type}, {Request}",
                request.GetType().FullName, JsonSerializer.Serialize(request, options: jsonOptions));

            return Task.CompletedTask;
        }
    }
}