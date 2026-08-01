using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using TesisGestorApi.DTOs;
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

        public async Task SendAsync(
            string to,
            string subject,
            string htmlBody,
            CancellationToken ct = default,
            IEnumerable<EmailAttachmentDto>? attachments = null,
            IEnumerable<EmailInlineResourceDto>? inlineResources = null)
        {
            var host = GetRequiredSetting("Email:SmtpHost");
            var portStr = GetRequiredSetting("Email:SmtpPort");
            var user = GetRequiredSetting("Email:User");
            var pass = GetRequiredSetting("Email:Pass");
            var from = GetRequiredSetting("Email:From");
            var enableSsl = GetOptionalBoolSetting("Email:EnableSsl", defaultValue: true);

            if (!int.TryParse(portStr, out var port))
                throw new Exception("Email:SmtpPort inválido");

            if (host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
                pass = pass.Replace(" ", string.Empty);

            using var client = new SmtpClient(host, port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = enableSsl
            };

            using var mail = new MailMessage(from, to, subject, htmlBody)
            {
                IsBodyHtml = true
            };

            var resources = inlineResources?
                .Where(r => r.Content.Length > 0 && !string.IsNullOrWhiteSpace(r.ContentId))
                .ToList();

            if (resources is { Count: > 0 })
            {
                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);

                foreach (var resource in resources)
                {
                    var linkedResource = new LinkedResource(new MemoryStream(resource.Content), resource.ContentType)
                    {
                        ContentId = resource.ContentId,
                        TransferEncoding = TransferEncoding.Base64
                    };

                    htmlView.LinkedResources.Add(linkedResource);
                }

                mail.AlternateViews.Add(htmlView);
            }

            var files = attachments?
                .Where(a => a.Content.Length > 0 && !string.IsNullOrWhiteSpace(a.FileName))
                .ToList();

            if (files is { Count: > 0 })
            {
                foreach (var attachment in files)
                {
                    mail.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));
                }
            }

            await client.SendMailAsync(mail, ct);
        }

        private string GetRequiredSetting(string key)
        {
            var value = _config[key]?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Falta configurar {key}");

            return value;
        }

        private bool GetOptionalBoolSetting(string key, bool defaultValue)
        {
            var value = _config[key]?.Trim();
            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }
    }
}
