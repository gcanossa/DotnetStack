using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace GKit.SmtpHost
{
    public class SmtpHostOptions
    {
        [Required]
        [Range(25, 65535)]
        public int Port { get; set; } = 9025;
        public string DeadLettersPath { get; set; } = "./dead-letters";

        /// <summary>
        /// Username to password, used by <see cref="ConfigurationSmtpIdentityStore"/>.
        /// Empty by default: no credential is valid until one is configured.
        /// </summary>
        public Dictionary<string, string> Users { get; set; } = [];

        public string? CertificatePem { get; set; } = null;
        public string? CertificateKeyPem { get; set; } = null;
    }
}