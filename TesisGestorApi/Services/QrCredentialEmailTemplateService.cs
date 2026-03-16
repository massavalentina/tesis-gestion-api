using System.Text;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class QrCredentialEmailTemplateService : IQrCredentialEmailTemplateService
    {
        public string Build(
            string tutorNombre,
            string alumnoNombre,
            int anioLectivo,
            Guid codigoQr,
            string? mensajePersonalizado,
            string qrInlineContentId)
        {
            var saludoTutor = string.IsNullOrWhiteSpace(tutorNombre) ? "Tutor/a" : tutorNombre.Trim();
            var nombreAlumno = string.IsNullOrWhiteSpace(alumnoNombre) ? "estudiante" : alumnoNombre.Trim();

            var mensaje = string.IsNullOrWhiteSpace(mensajePersonalizado)
                ? "Adjuntamos la credencial QR para registrar asistencia del estudiante."
                : mensajePersonalizado.Trim();

            var html = new StringBuilder();
            html.Append("<html><body style=\"margin:0;padding:0;background:#f6f8fb;font-family:Arial,Helvetica,sans-serif;color:#1f2937;\">");
            html.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"padding:20px 10px;\"><tr><td align=\"center\">");
            html.Append("<table width=\"620\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:620px;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;\">");
            html.Append("<tr><td style=\"background:#0f4c81;color:#ffffff;padding:18px 22px;font-size:20px;font-weight:700;\">Credencial QR Escolar</td></tr>");
            html.Append("<tr><td style=\"padding:22px;\">");
            html.Append($"<p style=\"margin:0 0 12px 0;\">Hola <strong>{saludoTutor}</strong>,</p>");
            html.Append($"<p style=\"margin:0 0 12px 0;line-height:1.5;\">{mensaje}</p>");
            html.Append($"<p style=\"margin:0 0 18px 0;line-height:1.5;\">Alumno/a: <strong>{nombreAlumno}</strong><br/>Año lectivo: <strong>{anioLectivo}</strong></p>");
            html.Append("<div style=\"text-align:center;padding:10px 0 18px 0;\">");
            html.Append($"<img src=\"cid:{qrInlineContentId}\" alt=\"QR credencial\" style=\"width:220px;height:220px;border:1px solid #d1d5db;border-radius:8px;padding:8px;background:#fff;\" />");
            html.Append("</div>");
            html.Append($"<p style=\"margin:0 0 10px 0;font-size:13px;color:#4b5563;\">Código de respaldo: <strong>{codigoQr}</strong></p>");
            html.Append("<p style=\"margin:0;font-size:12px;color:#6b7280;\">Este email fue enviado automáticamente por Tesis Gestor.</p>");
            html.Append("</td></tr></table>");
            html.Append("</td></tr></table></body></html>");

            return html.ToString();
        }
    }
}
