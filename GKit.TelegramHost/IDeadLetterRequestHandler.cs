using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    public interface IDeadLetterRequestHandler
    {
        Task Handle(IServiceProvider provider, TL.IObject request);
    }
}