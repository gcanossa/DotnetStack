using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GKit.TelegramHost
{
    public class ControllerRouteRequestHandler : IRequestHandler
    {
        
        private readonly ILogger<ControllerRouteRequestHandler> _logger;
        public ControllerRouteRequestHandler(ILogger<ControllerRouteRequestHandler> logger)
        {
            _logger = logger;
        }

        public async Task<bool> Handle(IServiceProvider provider, TL.IObject request)
        {
            var controllers = SelectControllers(request);

            if(controllers.Count == 0)
            {
                return await Task.FromResult(false);
            }
            else
            {
                foreach(var controller in controllers){
                    var obj = (TelegramControllerBase)provider.GetRequiredService(controller.Key);
                    //TODO: assign context info
                    using var controllerScope = _logger.BeginScope(new { Controller = controller.Key.FullName });

                    foreach(var method in controller.Value)
                    {
                        using var methodScope = _logger.BeginScope(new { Action = method.Name });
                        try{
                            var task = (Task)method.Invoke(obj, new[] { request })!;
                            await task;

                            _logger.LogInformation("Success");
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
        
        protected Dictionary<Type, IEnumerable<MethodInfo>> SelectControllers(TL.IObject request)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(p => p.GetTypes())
                .Where(p => p.IsAssignableTo(typeof(TelegramControllerBase)) && !p.IsAbstract)
                .SelectMany(p => p.GetMethods().Where(m => 
                        m.IsPublic && 
                        !m.IsAbstract && 
                        m.ReturnType.IsAssignableTo(typeof(Task)) && 
                        m.GetParameters().Length == 1 && 
                        m.GetParameters()[0].ParameterType.IsAssignableFrom(request.GetType()) &&
                        m.GetCustomAttribute<TelegramRouteAttribute>() != null  &&
                        (m.GetCustomAttribute<TelegramRouteAttribute>()!.EventType ?? request.GetType()) == request.GetType()))
                .GroupBy(p=>p.DeclaringType!, p=>p)
                .ToDictionary(p=>p.Key, p=>p.AsEnumerable());
        }
    }
}