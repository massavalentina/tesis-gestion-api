    using System.Net;
    using System.Net.Mail;
    using TesisGestorApi.Interfaces;

    namespace TesisGestorApi.Services
    {
        public class SmtpEmailSender : IEmailSender
        {
            private readonly IConfiguration _config;

            public SmtpEmailSender(IConfiguration config)
            {
                _config = config;
            }

            public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
            {
                var host = _config["Email:SmtpHost"] ?? throw new Exception("Falta Email:SmtpHost");
                var portStr = _config["Email:SmtpPort"] ?? throw new Exception("Falta Email:SmtpPort");
                var user = _config["Email:User"] ?? throw new Exception("Falta Email:User");
                var pass = _config["Email:Pass"] ?? throw new Exception("Falta Email:Pass");
                var from = _config["Email:From"] ?? throw new Exception("Falta Email:From");

                if (!int.TryParse(portStr, out var port))
                    throw new Exception("Email:SmtpPort inválido");

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(user, pass),
                    EnableSsl = true
                };

                using var mail = new MailMessage(from, to, subject, htmlBody)
                {
                    IsBodyHtml = true
                };

                await client.SendMailAsync(mail);
            }
        }
    }
