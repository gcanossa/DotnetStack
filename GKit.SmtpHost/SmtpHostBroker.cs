using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks;
using MimeKit;
using SmtpServer;

namespace GKit.SmtpHost
{
    internal class SmtpHostBroker : ISmtpHostBroker
    {
        protected Channel<(MimeMessage, ISessionContext)> MessageSink { get; init; } = Channel.CreateUnbounded<(MimeMessage, ISessionContext)>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
            AllowSynchronousContinuations = true
        });
        

        protected ChannelReader<(MimeMessage, ISessionContext)> Reader => MessageSink.Reader;
        protected ChannelWriter<(MimeMessage, ISessionContext)> Writer => MessageSink.Writer;


        public ValueTask EnqueueAsync(MimeMessage message, ISessionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Writer.WriteAsync(new (message, context), cancellationToken);
        }

        public async ValueTask<(MimeMessage, ISessionContext)> DequeueAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Reader.ReadAsync();
        }

        public async IAsyncEnumerable<(MimeMessage, ISessionContext)> ProcessAllAsync([EnumeratorCancellation]CancellationToken cancellationToken = default(CancellationToken))
        {
            while (await Reader.WaitToReadAsync(cancellationToken))
            {
                while (Reader.TryRead(out (MimeMessage Message, ISessionContext Context) item))
                {
                    yield return item;
                }
            }
        }
    }
}