using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace GKit.SmtpHost
{
    internal class SmtpMessageStore : MessageStore
    {
        internal ISmtpHostBroker Broker { get; init; }

        public SmtpMessageStore(ISmtpHostBroker broker)
        {
            Broker = broker;
        }

        public override async Task<SmtpResponse> SaveAsync(
            ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer,
            CancellationToken cancellationToken)
        {
            await using var stream = new MemoryStream();

            var position = buffer.GetPosition(0);
            while (buffer.TryGet(ref position, out var memory))
            {
                await stream.WriteAsync(memory, cancellationToken);
            }

            stream.Position = 0;

            var message = await MimeKit.MimeMessage.LoadAsync(stream, cancellationToken);

            await Broker.EnqueueAsync(message, context, cancellationToken);

            return SmtpResponse.Ok;
        }
    }
}