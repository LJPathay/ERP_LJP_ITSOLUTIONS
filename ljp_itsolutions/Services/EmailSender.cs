using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;

namespace ljp_itsolutions.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpHost = _config["Smtp:Host"];
            if (string.IsNullOrEmpty(smtpHost))
            {
                Console.WriteLine($"[Email] To:{to} Subject:{subject}\n{body}");
                return;
            }

            var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 25;
            var from = _config["Smtp:From"] ?? "no-reply@coffee.local";

            using var client = new SmtpClient(smtpHost, port);
            var user = _config["Smtp:User"];
            var pass = _config["Smtp:Password"];
            if (!string.IsNullOrEmpty(user))
            {
                client.Credentials = new System.Net.NetworkCredential(user, pass);
                client.EnableSsl = true;
            }

            var mail = new MailMessage(from, to, subject, body) { IsBodyHtml = false };
            await client.SendMailAsync(mail);
        }
    }
}
