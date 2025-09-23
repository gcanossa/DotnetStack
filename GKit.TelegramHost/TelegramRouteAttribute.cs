using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TelegramRouteAttribute : Attribute
    {
        public Type EventType { get; init; }

        public TelegramRouteAttribute(Type eventType = null)
        {
            EventType = eventType;
        }
    }
}