using System.Reflection;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    /// <summary>
    /// The set of controller actions that can handle a message, discovered once.
    /// <para>
    /// <c>ControllerRouteMessageHandler</c> used to walk every assembly in the AppDomain and
    /// every type in them <em>per message</em> — hundreds of thousands of reflection operations
    /// per mail, and a <see cref="ReflectionTypeLoadException"/> away from failing outright.
    /// </para>
    /// </summary>
    public class SmtpRouteTable
    {
        private readonly List<(Type Controller, MethodInfo Action, SmtpRouteAttribute Route)> _routes;

        public SmtpRouteTable(IEnumerable<Assembly> assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            _routes = [.. assemblies.Distinct().SelectMany(SafeGetTypes)
                .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(SmtpControllerBase)))
                .SelectMany(t => t.GetMethods()
                    .Where(m => m.IsPublic && !m.IsAbstract &&
                                m.ReturnType.IsAssignableTo(typeof(Task)) &&
                                m.GetParameters().Length == 1 &&
                                m.GetParameters()[0].ParameterType.IsAssignableFrom(typeof(MimeMessage)))
                    .SelectMany(m => m.GetCustomAttributes<SmtpRouteAttribute>()
                        .Select(route => (Controller: t, Action: m, Route: route))))];
        }

        public static SmtpRouteTable FromAppDomain() =>
            new(AppDomain.CurrentDomain.GetAssemblies());

        public IReadOnlyCollection<Type> ControllerTypes => [.. _routes.Select(r => r.Controller).Distinct()];

        public int Count => _routes.Count;

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t is not null).Select(t => t!);
            }
        }

        /// <summary>Controller type to the actions that match, preserving declaration order.</summary>
        public Dictionary<Type, IEnumerable<MethodInfo>> Resolve(MimeMessage message, ISessionContext? context)
        {
            ArgumentNullException.ThrowIfNull(message);

            return _routes
                .Where(r => r.Route.IsMatch(message, context))
                .GroupBy(r => r.Controller, r => r.Action)
                .ToDictionary(g => g.Key, g => g.Distinct());
        }
    }
}
