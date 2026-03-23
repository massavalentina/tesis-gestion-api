using System.Net;
using System.Text;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class QrCredentialEmailTemplateService : IQrCredentialEmailTemplateService
    {
        public string Build(
            string tutorNombre,
            string alumnoNombre,
            string alumnoDni,
            int anioLectivo,
            Guid codigoQr,
            DateTime fechaVigencia,
            string? mensajePersonalizado,
            string qrInlineContentId,
            string? logoInlineContentId = null)
        {
            var saludoTutor = string.IsNullOrWhiteSpace(tutorNombre) ? "Tutor/a" : tutorNombre.Trim();
            var nombreAlumno = string.IsNullOrWhiteSpace(alumnoNombre) ? "estudiante" : alumnoNombre.Trim();
            var dniAlumno = string.IsNullOrWhiteSpace(alumnoDni) ? "-" : alumnoDni.Trim();

            var mensaje = string.IsNullOrWhiteSpace(mensajePersonalizado)
                ? "Este código es personal e intransferible, por lo que le solicitamos su colaboración para conservarlo en buen estado y facilitarlo al estudiante cuando sea necesario."
                : mensajePersonalizado.Trim();

            var tutorHtml = WebUtility.HtmlEncode(saludoTutor);
            var alumnoHtml = WebUtility.HtmlEncode(nombreAlumno);
            var dniHtml = WebUtility.HtmlEncode(dniAlumno);
            var mensajeHtml = WebUtility.HtmlEncode(mensaje)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "<br />");
            var codigoHtml = WebUtility.HtmlEncode(codigoQr.ToString());
            var vigenciaTexto = fechaVigencia.ToString("dd/MM/yyyy");
            var anioLectivoHtml = WebUtility.HtmlEncode(anioLectivo.ToString());
            var qrContentIdHtml = WebUtility.HtmlEncode(qrInlineContentId);

            var logoBlock = string.IsNullOrWhiteSpace(logoInlineContentId)
                ? string.Empty
                : $"<div style=\"text-align:center;padding:12px 0 8px 0;\"><img src=\"cid:{WebUtility.HtmlEncode(logoInlineContentId)}\" alt=\"Logo institucional\" style=\"height:58px;width:auto;display:inline-block;\" /></div>";

            var html = new StringBuilder();
            html.Append("<html><body style=\"margin:0;padding:14px 0 20px 0;background:#ffffff;font-family:Arial,Helvetica,sans-serif;color:#1f1f1f;font-style:italic;font-size:9pt;\">");
            html.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\"><tr><td align=\"center\">");
            html.Append("<table width=\"640\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"max-width:640px;background:#ffffff;padding:0 56px 14px 56px;\">");
            html.Append("<tr><td>");
            html.Append(logoBlock);
            html.Append("<div style=\"border-top:1px solid #b5b5b5;margin:0 0 22px 0;\"></div>");
            html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Estimado/a <strong>{tutorHtml}</strong>:</p>");
            html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Por medio del presente le hacemos llegar la credencial QR personal asignada al estudiante <strong>{alumnoHtml}</strong> cuyo DNI es <strong>{dniHtml}</strong>, el cual será utilizado para el registro de asistencia en la institución.</p>");
            html.Append($"<p style=\"margin:0 0 16px 0;font-size:9pt;line-height:1.38;\">{mensajeHtml}</p>");
            html.Append("<p style=\"margin:0 0 8px 0;font-size:9pt;line-height:1.38;\">A continuación, le brindamos información importante sobre su uso:</p>");
            html.Append("<ul style=\"margin:0 0 14px 24px;padding:0;font-size:9pt;line-height:1.34;\">");
            html.Append("<li>La credencial siempre deberá ser presentada en la institución.</li>");
            html.Append("<li>Tiene una fecha de vencimiento, luego de la cual dejará de ser válida.</li>");
            html.Append("<li>En caso de extravío o inconvenientes, podrá solicitar el reenvío del código por medio de canales institucionales o acercándose a la institución.</li>");
            html.Append("</ul>");
            html.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Agradecemos su compromiso y colaboración para garantizar el correcto funcionamiento de este sistema. Ante cualquier duda o consulta, no dude en comunicarse con la institución.</p>");
            html.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"margin:0 0 8px 0;\">");
            html.Append("<tr>");
            html.Append("<td style=\"width:62%;vertical-align:top;padding-right:12px;\">");
            html.Append("<p style=\"margin:0;font-size:9pt;line-height:1.34;\">Atentamente,</p>");
            html.Append("<p style=\"margin:0 0 2px 0;font-size:9pt;line-height:1.34;\">Atentamente,</p>");
            html.Append("<p style=\"margin:0;font-size:9pt;line-height:1.34;\">Colegio Luis Manuel Robles</p>");
            html.Append($"<p style=\"margin:10px 0 0 0;font-size:9pt;line-height:1.35;color:#4d4d4d;\">Código de respaldo: <strong>{codigoHtml}</strong></p>");
            html.Append($"<p style=\"margin:5px 0 0 0;font-size:9pt;line-height:1.35;color:#4d4d4d;\">Año lectivo: <strong>{anioLectivoHtml}</strong></p>");
            html.Append("</td>");
            html.Append("<td align=\"center\" style=\"width:38%;vertical-align:top;\">");
            html.Append($"<img src=\"cid:{qrContentIdHtml}\" alt=\"QR credencial\" style=\"display:block;width:158px;height:158px;border:1px solid #9f9f9f;background:#fff;padding:4px;margin:0 auto;\" />");
            html.Append($"<p style=\"margin:8px 0 0 0;font-size:9pt;line-height:1.15;\">Vigencia hasta el {vigenciaTexto}</p>");
            html.Append("</td>");
            html.Append("</tr>");
            html.Append("</table>");
            html.Append("<div style=\"border-top:1px solid #b5b5b5;margin:16px 0 12px 0;\"></div>");
            html.Append("<p style=\"margin:0;text-align:center;font-size:8pt;line-height:1.35;color:#6e6e6e;\">Secretaría de la institución Colegio Luis Manuel RoblesPadre Luis Monti 1859, X5004ENI Córdoba - 03514517213 - <u>colegiorobles.edu.ar</u></p>");
            html.Append("<p style=\"margin:10px 0 0 0;text-align:center;font-size:8pt;line-height:1.3;color:#8a8a8a;\">Desde © PaletApp ");
            html.Append(DateTime.UtcNow.Year);
            html.Append("</p>");
            html.Append("</td></tr></table>");
            html.Append("</td></tr></table></body></html>");

            return html.ToString();
        }
    }
}
