using TesisGestorApi.DTOs;

namespace TesisGestorApi.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(
            string to,
            string subject,
            string htmlBody,
            CancellationToken ct = default,
            IEnumerable<EmailAttachmentDto>? attachments = null,
            IEnumerable<EmailInlineResourceDto>? inlineResources = null);
    }
}
