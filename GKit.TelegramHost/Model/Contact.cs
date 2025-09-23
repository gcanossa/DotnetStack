using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost.Model
{
    public record Contact(long Id, long AccessHash, string FirstName);
}