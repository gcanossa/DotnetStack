using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    public static class HelperExtensions
    {
        //TODO: verify
        public static long GetTelegramHash(this IEnumerable<long> ids)
        {
            return ids.Aggregate(0L, (hash, id) => {
                hash = hash ^ (id >> 21);
                hash = hash ^ (id << 35);
                hash = hash ^ (id >> 4);
                return hash + id;
            });
        }

        public static long GetHash(this TL.Contacts_Contacts ext)
        {
            return ext.users.Select(p => p.Key).Order().GetTelegramHash();
        }
    }
}