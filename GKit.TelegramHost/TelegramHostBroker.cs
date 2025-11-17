using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    public class TelegramHostBroker : ITelegramHostBroker
    {
        
        protected Channel<TL.IObject> MessageSink { get; init; } = Channel.CreateUnbounded<TL.IObject>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
            AllowSynchronousContinuations = true
        });
        

        protected ChannelReader<TL.IObject> Reader => MessageSink.Reader;
        protected ChannelWriter<TL.IObject> Writer => MessageSink.Writer;


        public ValueTask EnqueueAsync(TL.IObject request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Writer.WriteAsync(request, cancellationToken);
        }

        public async ValueTask<TL.IObject> DequeueAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Reader.ReadAsync();
        }

        public async IAsyncEnumerable<TL.IObject> ProcessAllAsync([EnumeratorCancellation]CancellationToken cancellationToken = default(CancellationToken))
        {
            while (await Reader.WaitToReadAsync(cancellationToken))
            {
                while (Reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
    }
}