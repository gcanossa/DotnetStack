using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.TelegramHost
{
    public class TelegramHostOptions
    {
        public string AppId { get; set; }
        public string AppHash { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        public string SessionFilePath { get; set; } = "./telegram_session.dat";

    }
}