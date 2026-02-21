using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Collections.Generic;
using System.IO;

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
            await SendEmailAsync(to, subject, body, new Dictionary<string, byte[]>());
        }

        public async Task SendEmailAsync(string to, string subject, string body, Dictionary<string, byte[]> attachments)
        {
            var smtpHost = _config["Smtp:Host"];
            if (string.IsNullOrEmpty(smtpHost))
            {
                Console.WriteLine($"[Email Simulation] To: {to}, Subject: {subject}");
                return;
            }

            var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
            var from = _config["Smtp:From"] ?? "no-reply@ljp-coffee.local";
            var user = _config["Smtp:User"];
            var pass = _config["Smtp:Password"];

            using (var client = new SmtpClient(smtpHost, port))
            {
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                
                if (!string.IsNullOrEmpty(user))
                {
                    client.Credentials = new System.Net.NetworkCredential(user, pass);
                }

                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(from);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;
                    mail.To.Add(to);

                    foreach (var attachment in attachments)
                    {
                        var ms = new MemoryStream(attachment.Value);
                        mail.Attachments.Add(new Attachment(ms, attachment.Key, "text/csv"));
                    }

                    client.Timeout = 20000; // 20 seconds timeout
                    Console.WriteLine($"[SMTP]: Attempting to send email to {to} via {smtpHost}...");
                    await client.SendMailAsync(mail);
                    Console.WriteLine($"[SMTP]: Success! Email sent to {to}.");
                }
            }
        }
    }
}
