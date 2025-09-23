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
        public ControllerRouteMessageHandler(ILogger<ControllerRouteMessageHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(IServiceProvider provider, MimeMessage message, ISessionContext context)
        {
            var controllers = SelectControllers(message, context);

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
                            var task = (Task)method.Invoke(obj, new[] { message })!;

                            if(task is Task<SmtpControllerActionResult>)
                            {
                                var result = await (Task<SmtpControllerActionResult>)task switch
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
                        catch(SmtpSkippedActionException e)
                        {
                            _logger.LogInformation("Skipped by controller logic");
                            return await Task.FromResult(false);
                        }
                        catch(SmtpFailedActionException e)
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

        protected Dictionary<Type, IEnumerable<MethodInfo>> SelectControllers(MimeMessage message, ISessionContext context)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(p => p.GetTypes())
                .Where(p => p.IsAssignableTo(typeof(SmtpControllerBase)) && !p.IsAbstract)
                .SelectMany(p => p.GetMethods().Where(m => 
                        m.IsPublic && 
                        !m.IsAbstract && 
                        m.ReturnType.IsAssignableTo(typeof(Task)) && 
                        m.GetParameters().Length == 1 && 
                        m.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(MimeMessage)) &&
                        (m.GetCustomAttribute<SmtpRouteAttribute>()?.IsMatch(message, context) ?? false)))
                .GroupBy(p=>p.DeclaringType!, p=>p)
                .ToDictionary(p=>p.Key, p=>p.AsEnumerable());
        }
    }
}