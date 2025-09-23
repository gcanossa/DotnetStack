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

        public string? CertificatePem { get; set; } = null;
        public string? CertificateKeyPem { get; set; } = null;
    }
}