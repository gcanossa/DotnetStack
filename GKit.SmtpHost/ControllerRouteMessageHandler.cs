using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    public class ControllerRouteMessageHandler : IMessageHandler
    {
        private readonly ILogger<ControllerRouteMessageHandler> _logger;
        private readonly SmtpRouteTable _routes;

        public ControllerRouteMessageHandler(ILogger<ControllerRouteMessageHandler> logger, SmtpRouteTable routes)
        {
            _logger = logger;
            _routes = routes;
        }

        public async Task<bool> Handle(IServiceProvider provider, MimeMessage message, ISessionContext context)
        {
            var controllers = _routes.Resolve(message, context);

            if(controllers.Count == 0)
            {
                return await Task.FromResult(false);
            }
            else
            {
                foreach(var controller in controllers){
                    var obj = (SmtpControllerBase)provider.GetRequiredService(controller.Key);
                    obj.Context = context;

                    using var controllerScope = _logger.BeginScope(new { Controller = controller.Key.FullName });
                    foreach(var method in controller.Value)
                    {
                        using var methodScope = _logger.BeginScope(new { Action = method.Name });
                        try{
                            var task = (Task)method.Invoke(obj, [message])!;

                            if(task is Task<SmtpControllerActionResult> tsk)
                            {
                                var result = await tsk switch
                                {
                                    SmtpControllerActionResult.Failure => throw new SmtpFailedActionException(),
                                    SmtpControllerActionResult.Skipped => throw new SmtpSkippedActionException(),
                                    _ => SmtpControllerActionResult.Success
                                };
                            }
                            else
                            {
                                await task;
                            }

                            _logger.LogInformation("Success");
                        }
                        catch(SmtpSkippedActionException)
                        {
                            _logger.LogInformation("Skipped by controller logic");
                            return await Task.FromResult(false);
                        }
                        catch(SmtpFailedActionException)
                        {
                            _logger.LogWarning("Failed by controller logic");
                            return await Task.FromResult(false);
                        }
                        catch(Exception e){
                            _logger.LogError(e, "Fail");
                            return await Task.FromResult(false);
                        }
                    }
                }

                return await Task.FromResult(true);
            }
        }

    }
}