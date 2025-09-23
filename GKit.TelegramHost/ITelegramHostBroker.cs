using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    public interface ITelegramHostBroker
    {
        
        ValueTask EnqueueAsync(TL.IObject request, CancellationToken cancellationToken);
        
        ValueTask<TL.IObject> DequeueAsync(CancellationToken cancellationToken);

        IAsyncEnumerable<TL.IObject> ProcessAllAsync(CancellationToken cancellationToken);
    }
}