using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class AsistenciaUmbralService : IAsistenciaUmbralService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AsistenciaUmbralService> _logger;

        private static readonly int[] UMBRALES = new[] { 10, 15, 20, 25 };
        private const string PDF_URL = "https://www.cba.gov.ar/wp-content/uploads/2019/11/318-Gestionar-Asistencias-NRA.pdf";
        private const string LOGO_CONTENT_ID = "institution-logo";
        private static readonly Lazy<byte[]?> _logoBytes = new(LoadInstitutionLogo);

        public AsistenciaUmbralService(
            ApplicationDbContext db,
            IEmailSender emailSender,
            ILogger<AsistenciaUmbralService> logger)
        {
            _db = db;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task ProcesarUmbralesAsync(List<Guid> estudiantesIds, int anioLectivo, CancellationToken ct = default)
        {
            var ahora = DateTime.UtcNow;

            foreach (var estId in estudiantesIds.Distinct())
            {
                var faltas = await _db.Asistencias
                    .Where(a => a.EstudianteId == estId && a.Fecha.Year == anioLectivo)
                    .SumAsync(a => a.ValorTotalInasistencia, ct);

                var resumen = await _db.AsistenciasResumenAnual
                    .FirstOrDefaultAsync(r => r.IdEstudiante == estId && r.AnioLectivo == anioLectivo, ct);

                // Guardar cuántas faltas tenía ANTES de este lote para saber qué umbrales son nuevos
                var faltasAntes = resumen?.FaltasAcumuladas ?? 0m;

                if (resumen == null)
                {
                    resumen = new AsistenciaResumenAnual
                    {
                        IdResumen = Guid.NewGuid(),
                        IdEstudiante = estId,
                        AnioLectivo = anioLectivo
                    };
                    _db.AsistenciasResumenAnual.Add(resumen);
                }

                resumen.FaltasAcumuladas = faltas;
                resumen.UltimoRecalculoUtc = ahora;

                var alumno = await _db.Estudiantes.FirstOrDefaultAsync(e => e.IdEstudiante == estId, ct);
                if (alumno != null)
                {
                    if (faltas >= 25m)
                    {
                        resumen.TeaGeneral = true;
                        resumen.FechaTeaGeneralUtc ??= ahora;
                        alumno.TeaGeneral = true;
                    }
                    else
                    {
                        resumen.TeaGeneral = false;
                        resumen.FechaTeaGeneralUtc = null;
                        alumno.TeaGeneral = false;
                    }
                }

                await _db.SaveChangesAsync(ct);

                foreach (var umbral in UMBRALES)
                {
                    if (faltas < umbral)
                        continue;

                    // El umbral ya estaba superado antes de este lote: no enviar
                    if (faltasAntes >= umbral)
                        continue;

                    var existe = await _db.AsistenciasUmbralNotificacion
                        .AnyAsync(n => n.IdEstudiante == estId && n.AnioLectivo == anioLectivo && n.Umbral == umbral, ct);

                    if (!existe)
                        await EnviarNotificacionAsync(estId, anioLectivo, umbral, alumno, ahora, ct);
                }
            }
        }

        private async Task EnviarNotificacionAsync(
            Guid estId, int anioLectivo, int umbral, Estudiante? alumno, DateTime ahora, CancellationToken ct)
        {
            var tutorInfo = await _db.Set<TutorEstudiante>()
                .Where(te => te.IdEstudiante == estId && te.EsPrincipal)
                .Select(te => new { te.Tutor.Correo, te.Tutor.Nombre, te.Tutor.Apellido })
                .FirstOrDefaultAsync(ct);

            if (tutorInfo == null || string.IsNullOrWhiteSpace(tutorInfo.Correo))
            {
                tutorInfo = await _db.Set<TutorEstudiante>()
                    .Where(te => te.IdEstudiante == estId)
                    .Select(te => new { te.Tutor.Correo, te.Tutor.Nombre, te.Tutor.Apellido })
                    .FirstOrDefaultAsync(ct);
            }

            if (tutorInfo == null || string.IsNullOrWhiteSpace(tutorInfo.Correo))
            {
                _logger.LogWarning("Estudiante {EstId}: umbral {Umbral} sin tutor con correo registrado.", estId, umbral);
                return;
            }

            var nombreAlumno = alumno != null ? $"{alumno.Apellido}, {alumno.Nombre}" : estId.ToString();
            var tutorNombre = $"{tutorInfo.Nombre} {tutorInfo.Apellido}".Trim();

            var logoBytes = _logoBytes.Value;
            var logoId = logoBytes is { Length: > 0 } ? LOGO_CONTENT_ID : null;

            var (subject, body) = BuildEmailTemplate(umbral, tutorNombre, nombreAlumno, anioLectivo, logoId);

            List<EmailInlineResourceDto>? inlineResources = null;
            if (logoBytes is { Length: > 0 } && logoId != null)
            {
                inlineResources = new List<EmailInlineResourceDto>
                {
                    new() { ContentId = logoId, ContentType = "image/png", Content = logoBytes }
                };
            }

            try
            {
                await _emailSender.SendAsync(tutorInfo.Correo, subject, body, ct, inlineResources: inlineResources);

                _db.AsistenciasUmbralNotificacion.Add(new AsistenciaUmbralNotificacion
                {
                    IdNotif = Guid.NewGuid(),
                    IdEstudiante = estId,
                    AnioLectivo = anioLectivo,
                    Umbral = umbral,
                    CreadoUtc = ahora
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación umbral {Umbral} a {Email}", umbral, tutorInfo.Correo);
            }
        }

        private static (string subject, string body) BuildEmailTemplate(
            int umbral, string tutorNombre, string alumnoNombre, int anioLectivo, string? logoContentId)
        {
            var subject = umbral == 25
                ? $"Notificación TEA - {alumnoNombre} ({anioLectivo})"
                : $"Aviso de inasistencias - {alumnoNombre} - {umbral} faltas ({anioLectivo})";

            var tutorHtml = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(tutorNombre) ? "Tutor/a" : tutorNombre);
            var alumnoHtml = WebUtility.HtmlEncode(alumnoNombre);
            var pdfLink = $"<a href=\"{PDF_URL}\" style=\"color:#1565c0;font-style:italic;\">Gestión de Asistencia Ministerio de Educación de la Provincia de Córdoba</a>";

            var logoBlock = string.IsNullOrWhiteSpace(logoContentId)
                ? string.Empty
                : $"<div style=\"text-align:center;padding:12px 0 8px 0;\"><img src=\"cid:{WebUtility.HtmlEncode(logoContentId)}\" alt=\"Logo institucional\" style=\"height:58px;width:auto;display:inline-block;\" /></div>";

            var html = new StringBuilder();
            html.Append("<html><body style=\"margin:0;padding:14px 0 20px 0;background:#ffffff;font-family:Arial,Helvetica,sans-serif;color:#1f1f1f;font-style:italic;font-size:9pt;\">");
            html.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\"><tr><td align=\"center\">");
            html.Append("<table width=\"640\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"max-width:640px;background:#ffffff;padding:0 56px 14px 56px;\">");
            html.Append("<tr><td>");
            html.Append(logoBlock);
            html.Append("<div style=\"border-top:1px solid #b5b5b5;margin:0 0 22px 0;\"></div>");
            html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Estimado/a <strong>{tutorHtml}</strong>:</p>");

            if (umbral == 25)
            {
                html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Le informamos que el/la estudiante <strong>{alumnoHtml}</strong> ha alcanzado 25 inasistencias en el presente ciclo lectivo. Según la normativa institucional, esto implica el paso a condición <strong>TEA (Trayectoria Escolar Asistida)</strong>.</p>");
                html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Podrá encontrar información ampliada sobre esta condición en el siguiente enlace:<br />{pdfLink}</p>");
                html.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Asimismo, lo invitamos a acercarse a la institución para recibir una explicación completa sobre las implicancias y los pasos a seguir.</p>");
                html.Append("<p style=\"margin:0 0 22px 0;font-size:9pt;line-height:1.38;\">Quedamos a disposición para coordinar una reunión si así lo desea.</p>");
            }
            else
            {
                html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Nos comunicamos desde el Colegio Luis Manuel Robles para informarle que el/la estudiante <strong>{alumnoHtml}</strong> ha alcanzado <strong>{umbral} inasistencias</strong> en el presente ciclo lectivo.</p>");
                html.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Recordamos que al alcanzar las 25 inasistencias, el/la estudiante pasará automáticamente a condición <strong>TEA (Trayectoria Escolar Asistida)</strong>.</p>");
                html.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Podrá encontrar información ampliada sobre esta condición en el siguiente enlace:<br />{pdfLink}</p>");
                html.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Asimismo, si ustedes lo desea, lo invitamos a acercarse a la institución para recibir una explicación completa sobre las implicancias.</p>");
                html.Append("<p style=\"margin:0 0 22px 0;font-size:9pt;line-height:1.38;\">Quedamos a su disposición.</p>");
            }

            html.Append("<p style=\"margin:0 0 2px 0;font-size:9pt;line-height:1.34;\">Atentamente,</p>");
            html.Append("<p style=\"margin:0 0 22px 0;font-size:9pt;line-height:1.34;\">Colegio Luis Manuel Robles</p>");
            html.Append("<div style=\"border-top:1px solid #b5b5b5;margin:16px 0 12px 0;\"></div>");
            html.Append("<p style=\"margin:0;text-align:center;font-size:8pt;line-height:1.35;color:#6e6e6e;\">Secretaría de la institución Colegio Luis Manuel Robles · Padre Luis Monti 1859, X5004ENI Córdoba - 03514517213 - <u>colegiorobles.edu.ar</u></p>");
            html.Append($"<p style=\"margin:10px 0 0 0;text-align:center;font-size:8pt;line-height:1.3;color:#8a8a8a;\">Desde © PaletApp {DateTime.UtcNow.Year}</p>");
            html.Append("</td></tr></table>");
            html.Append("</td></tr></table></body></html>");

            return (subject, html.ToString());
        }

        private static byte[]? LoadInstitutionLogo()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "utils", "robles.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "utils", "robles.png")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(candidate);
                    if (File.Exists(fullPath))
                        return File.ReadAllBytes(fullPath);
                }
                catch { }
            }

            return null;
        }
    }
}
