using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    public class TelegramHostOptions
    {
        /// <summary>
        /// Opt in to connecting. Off by default so a misconfigured or banned account degrades
        /// to "does nothing and says so" rather than taking the host down at startup.
        /// </summary>
        public bool Enabled { get; set; }

        public string? AppId { get; set; }
        public string? AppHash { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        public string SessionFilePath { get; set; } = "./telegram_session.dat";

    }
}