using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/ficha")]
    public class FichaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;   // Servicio de mail ya existente en el proyecto
        private readonly IConfiguration _config;      // Para leer el nombre de la institución

        public FichaController(
            ApplicationDbContext db,
            IEmailSender emailSender,
            IConfiguration config)
        {
            _db = db;
            _emailSender = emailSender;
            _config = config;
        }

        // GET /api/ficha/estudiante/{id}
        [HttpGet("estudiante/{id:guid}")]
        public async Task<IActionResult> GetFichaEstudiante(
            Guid id,
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            var esDocente = User.FindAll("roles").Any(c => c.Value == "Docente");
            if (esDocente)
            {
                var idUsuarioStr = User.FindFirstValue("idUsuario");
                if (idUsuarioStr == null) return Forbid();
                var idUsuario = Guid.Parse(idUsuarioStr);
                var docente = await _db.Docentes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);
                if (docente == null) return Forbid();
                var tieneAcceso = await _db.EspaciosCurriculares
                    .AnyAsync(ec => ec.IdDocente == docente.IdDocente &&
                                    ec.Curso.DetallesCursado.Any(dc => dc.IdEstudiante == id && dc.Estado), ct);
                if (!tieneAcceso) return Forbid();
            }

            var estudiante = await _db.Estudiantes
                .AsNoTracking()
                .Include(e => e.TutorEstudiantes)
                    .ThenInclude(te => te.Tutor)
                .Include(e => e.DetallesCursado)
                    .ThenInclude(dc => dc.Curso)
                .FirstOrDefaultAsync(e => e.IdEstudiante == id, ct);

            if (estudiante == null)
                return NotFound($"No se encontró el estudiante con ID {id}.");

            var credencial = await _db.CredencialesQR
                .AsNoTracking()
                .Where(c => c.IdEstudiante == id && c.AñoLectivo.Year == anioLectivo)
                .OrderByDescending(c => c.FechaGeneracion)
                .ThenByDescending(c => c.IdQR)
                .Select(c => new { c.Activo })
                .FirstOrDefaultAsync(ct);

            var cursado = estudiante.DetallesCursado
                .Where(dc => dc.Estado)
                .OrderByDescending(dc => dc.Curso.AñoLectivo)
                .FirstOrDefault();

            return Ok(new
            {
                idEstudiante = estudiante.IdEstudiante,
                nombre = estudiante.Nombre,
                apellido = estudiante.Apellido,
                documento = estudiante.Documento,
                fechaNacimiento = estudiante.FechaNacimiento,
                domicilio = estudiante.Domicilio,
                sexo = estudiante.Sexo == Sexo.Masculino ? "M" :
                       estudiante.Sexo == Sexo.Femenino  ? "F" : null,
                codigoCurso = cursado?.Curso.Codigo,
                credencialQrActiva = credencial == null ? (bool?)null : credencial.Activo,
                tutores = estudiante.TutorEstudiantes
                    .OrderByDescending(te => te.EsPrincipal)
                    .Select(te => new
                    {
                        idTutor = te.Tutor.IdTutor,
                        nombre = te.Tutor.Nombre,
                        apellido = te.Tutor.Apellido,
                        documento = te.Tutor.Documento,
                        telefono = te.Tutor.Telefono,
                        correo = te.Tutor.Correo,
                        relacionEstudiante = te.Tutor.RelacionEstudiante,
                        fechaNacimiento = te.Tutor.FechaNacimiento,
                        domicilio = te.Tutor.Domicilio,
                        disponibilidad = te.Tutor.Disponibilidad,
                        esPrincipal = te.EsPrincipal,
                        // Exponemos ambas fechas para que el frontend pueda calcular
                        // si el tutor está desactualizado y si ya fue notificado recientemente.
                        fechaUltimaActualizacion = te.Tutor.FechaUltimaActualizacion,
                        fechaUltimaNotificacion  = te.Tutor.FechaUltimaNotificacion
                    })
                    .ToList()
            });
        }

        // PUT /api/ficha/estudiante/{id}
        [HttpPut("estudiante/{id:guid}")]
        public async Task<IActionResult> UpdateEstudiante(
            Guid id,
            [FromBody] UpdateEstudianteDto req,
            CancellationToken ct = default)
        {
            var est = await _db.Estudiantes.FindAsync(new object[] { id }, ct);
            if (est == null) return NotFound();

            est.Nombre = req.Nombre;
            est.Apellido = req.Apellido;
            est.Documento = req.Documento;
            est.FechaNacimiento = DateTime.SpecifyKind(DateTime.Parse(req.FechaNacimiento), DateTimeKind.Utc);
            est.Domicilio = req.Domicilio;
            est.Sexo = req.Sexo switch
            {
                "M" => Sexo.Masculino,
                "F" => Sexo.Femenino,
                _   => default
            };

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        // PUT /api/ficha/tutor/{idTutor}
        [HttpPut("tutor/{idTutor:guid}")]
        public async Task<IActionResult> UpdateTutor(
            Guid idTutor,
            [FromBody] UpdateTutorDto req,
            CancellationToken ct = default)
        {
            var tutor = await _db.Tutores.FindAsync(new object[] { idTutor }, ct);
            if (tutor == null) return NotFound();

            tutor.Nombre = req.Nombre;
            tutor.Apellido = req.Apellido;
            tutor.Documento = req.Documento;
            tutor.Telefono = req.Telefono;
            tutor.Correo = req.Correo;
            tutor.RelacionEstudiante = req.RelacionEstudiante;
            tutor.Disponibilidad = req.Disponibilidad ?? string.Empty;
            tutor.Domicilio = req.Domicilio;

            // Al editar los datos del tutor se registra la fecha de actualización.
            // Esto reinicia el contador de 6 meses para la alerta de desactualización.
            tutor.FechaUltimaActualizacion = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        // POST /api/ficha/estudiante/{idEstudiante}/tutores
        [HttpPost("estudiante/{idEstudiante:guid}/tutores")]
        public async Task<IActionResult> AddTutor(
            Guid idEstudiante,
            [FromBody] CreateTutorDto req,
            CancellationToken ct = default)
        {
            var existe = await _db.Estudiantes.AnyAsync(e => e.IdEstudiante == idEstudiante, ct);
            if (!existe) return NotFound();

            if (req.EsPrincipal)
            {
                var principalesActuales = await _db.Set<TutorEstudiante>()
                    .Where(te => te.IdEstudiante == idEstudiante && te.EsPrincipal)
                    .ToListAsync(ct);
                foreach (var l in principalesActuales) l.EsPrincipal = false;
            }

            var vinculo = new TutorEstudiante
            {
                IdEstudiante = idEstudiante,
                EsPrincipal = req.EsPrincipal
            };

            var tutor = new Tutor
            {
                IdTutor = Guid.NewGuid(),
                Nombre = req.Nombre,
                Apellido = req.Apellido,
                Documento = req.Documento,
                Telefono = req.Telefono,
                Correo = req.Correo,
                RelacionEstudiante = req.RelacionEstudiante,
                Disponibilidad = req.Disponibilidad ?? string.Empty,
                Domicilio = req.Domicilio,
                FechaNacimiento = DateTime.SpecifyKind(DateTime.Parse(req.FechaNacimiento), DateTimeKind.Utc),
                // Al dar de alta un tutor se establece la fecha de actualización como hoy.
                FechaUltimaActualizacion = DateTime.UtcNow,
                TutorEstudiantes = new List<TutorEstudiante> { vinculo }
            };

            _db.Tutores.Add(tutor);
            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                idTutor = tutor.IdTutor,
                nombre = tutor.Nombre,
                apellido = tutor.Apellido,
                documento = tutor.Documento,
                telefono = tutor.Telefono,
                correo = tutor.Correo,
                relacionEstudiante = tutor.RelacionEstudiante,
                fechaNacimiento = tutor.FechaNacimiento,
                domicilio = tutor.Domicilio,
                disponibilidad = tutor.Disponibilidad,
                esPrincipal = vinculo.EsPrincipal,
                // También se expone en la respuesta para que el frontend lo refleje de inmediato
                fechaUltimaActualizacion = tutor.FechaUltimaActualizacion
            });
        }

        // DELETE /api/ficha/estudiante/{idEstudiante}/tutores/{idTutor}
        [HttpDelete("estudiante/{idEstudiante:guid}/tutores/{idTutor:guid}")]
        public async Task<IActionResult> RemoveTutor(
            Guid idEstudiante,
            Guid idTutor,
            CancellationToken ct = default)
        {
            var link = await _db.Set<TutorEstudiante>()
                .FirstOrDefaultAsync(te => te.IdEstudiante == idEstudiante && te.IdTutor == idTutor, ct);

            if (link == null) return NotFound();

            if (link.EsPrincipal)
                return BadRequest(new { error = "No se puede eliminar el tutor principal. Primero asigná otro tutor como principal." });

            var count = await _db.Set<TutorEstudiante>()
                .CountAsync(te => te.IdEstudiante == idEstudiante, ct);
            if (count <= 1)
                return BadRequest(new { error = "El estudiante debe tener al menos un tutor registrado." });

            _db.Set<TutorEstudiante>().Remove(link);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        // PATCH /api/ficha/estudiante/{idEstudiante}/tutores/{idTutor}/principal
        [HttpPatch("estudiante/{idEstudiante:guid}/tutores/{idTutor:guid}/principal")]
        public async Task<IActionResult> SetTutorPrincipal(
            Guid idEstudiante,
            Guid idTutor,
            CancellationToken ct = default)
        {
            // Incluimos la entidad Tutor para poder actualizar FechaUltimaActualizacion
            // del nuevo tutor principal.
            var links = await _db.Set<TutorEstudiante>()
                .Include(te => te.Tutor)
                .Where(te => te.IdEstudiante == idEstudiante)
                .ToListAsync(ct);

            if (!links.Any(te => te.IdTutor == idTutor))
                return NotFound();

            foreach (var te in links)
                te.EsPrincipal = te.IdTutor == idTutor;

            // Al cambiar el tutor principal se resetea la fecha de actualización
            // del nuevo principal, reiniciando el contador de 6 meses para la alerta.
            var nuevoLink = links.First(te => te.IdTutor == idTutor);
            nuevoLink.Tutor.FechaUltimaActualizacion = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        // POST /api/ficha/curso/{idCurso}/notificar-tutores-desactualizados
        [HttpPost("curso/{idCurso:guid}/notificar-tutores-desactualizados")]
        public async Task<IActionResult> NotificarTutoresCurso(
            Guid idCurso,
            CancellationToken ct = default)
        {
            var estudiantes = await _db.Estudiantes
                .Where(e => e.DetallesCursado.Any(dc => dc.IdCurso == idCurso && dc.Estado))
                .Include(e => e.TutorEstudiantes)
                    .ThenInclude(te => te.Tutor)
                .ToListAsync(ct);

            var limite = DateTime.UtcNow.AddMonths(-6);
            var nombreInstitucion = _config["Institucion:Nombre"] ?? "Colegio Luis Manuel Robles";
            var logoBytes = TryLoadLogo();

            int enviados = 0;
            int omitidos = 0;

            foreach (var estudiante in estudiantes)
            {
                var tutorLink = estudiante.TutorEstudiantes.FirstOrDefault(te => te.EsPrincipal);
                if (tutorLink == null) { omitidos++; continue; }

                var tutor = tutorLink.Tutor;

                if (tutor.FechaUltimaActualizacion >= limite) { omitidos++; continue; }
                if (tutor.FechaUltimaNotificacion.HasValue && tutor.FechaUltimaNotificacion >= limite)
                { omitidos++; continue; }

                var nombreAlumno = $"{estudiante.Apellido}, {estudiante.Nombre}";
                var nombreTutor = $"{tutor.Nombre} {tutor.Apellido}";

                var (htmlBody, inlineResources) = BuildNotificacionHtml(nombreTutor, nombreAlumno, nombreInstitucion, logoBytes);

                await _emailSender.SendAsync(
                    to: tutor.Correo,
                    subject: $"Actualización de datos de contacto – {estudiante.Apellido}, {estudiante.Nombre}",
                    htmlBody: htmlBody,
                    ct: ct,
                    inlineResources: inlineResources);

                tutor.FechaUltimaNotificacion = DateTime.UtcNow;
                enviados++;
            }

            if (enviados > 0)
                await _db.SaveChangesAsync(ct);

            var mensaje = enviados == 0
                ? "No hay tutores desactualizados en este curso."
                : $"Se enviaron {enviados} notificaci{(enviados == 1 ? "ón" : "ones")} correctamente.";

            return Ok(new NotificacionCursoResponseDto
            {
                Enviados = enviados,
                Omitidos = omitidos,
                Mensaje = mensaje
            });
        }

        // POST /api/ficha/estudiante/{idEstudiante}/notificar-tutor-desactualizado
        [HttpPost("estudiante/{idEstudiante:guid}/notificar-tutor-desactualizado")]
        public async Task<IActionResult> NotificarTutorDesactualizado(
            Guid idEstudiante,
            CancellationToken ct = default)
        {
            var estudiante = await _db.Estudiantes
                .Include(e => e.TutorEstudiantes)
                    .ThenInclude(te => te.Tutor)
                .FirstOrDefaultAsync(e => e.IdEstudiante == idEstudiante, ct);

            if (estudiante == null)
                return NotFound($"No se encontró el estudiante con ID {idEstudiante}.");

            var tutorLink = estudiante.TutorEstudiantes.FirstOrDefault(te => te.EsPrincipal);
            if (tutorLink == null)
                return BadRequest(new NotificacionTutorResponseDto
                {
                    Enviado = false,
                    Mensaje = "El estudiante no tiene tutor principal asignado."
                });

            var tutor = tutorLink.Tutor;
            var limiteDesactualizacion = DateTime.UtcNow.AddMonths(-6);

            if (tutor.FechaUltimaActualizacion >= limiteDesactualizacion)
                return BadRequest(new NotificacionTutorResponseDto
                {
                    Enviado = false,
                    Mensaje = "El tutor principal fue actualizado recientemente. No es necesario enviar notificación."
                });

            if (tutor.FechaUltimaNotificacion.HasValue && tutor.FechaUltimaNotificacion >= limiteDesactualizacion)
                return BadRequest(new NotificacionTutorResponseDto
                {
                    Enviado = false,
                    Mensaje = "Ya se envió una notificación de actualización en los últimos 6 meses."
                });

            var nombreInstitucion = _config["Institucion:Nombre"] ?? "Colegio Luis Manuel Robles";
            var logoBytes = TryLoadLogo();

            var nombreAlumno = $"{estudiante.Apellido}, {estudiante.Nombre}";
            var nombreTutor = $"{tutor.Nombre} {tutor.Apellido}";

            var (htmlBody, inlineResources) = BuildNotificacionHtml(nombreTutor, nombreAlumno, nombreInstitucion, logoBytes);

            await _emailSender.SendAsync(
                to: tutor.Correo,
                subject: $"Actualización de datos de contacto – {estudiante.Apellido}, {estudiante.Nombre}",
                htmlBody: htmlBody,
                ct: ct,
                inlineResources: inlineResources);

            tutor.FechaUltimaNotificacion = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Ok(new NotificacionTutorResponseDto
            {
                Enviado = true,
                Mensaje = $"Notificación enviada a {tutor.Correo}."
            });
        }

        // ── Helpers para el email de notificación ──────────────────────────────

        private const string LogoContentId = "logo-institucional-notif";
        private static readonly Lazy<byte[]?> _logoBytes = new(LoadLogo);

        private static byte[]? TryLoadLogo() => _logoBytes.Value;

        private static byte[]? LoadLogo()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "utils", "robles.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "utils", "robles.png")
            };

            foreach (var path in candidates)
            {
                try
                {
                    var full = Path.GetFullPath(path);
                    if (System.IO.File.Exists(full)) return System.IO.File.ReadAllBytes(full);
                }
                catch { /* si no hay logo, el mail se envía sin imagen */ }
            }

            return null;
        }

        private static (string html, IEnumerable<EmailInlineResourceDto>? inlineResources) BuildNotificacionHtml(
            string nombreTutor,
            string nombreAlumno,
            string nombreInstitucion,
            byte[]? logoBytes)
        {
            var tutorHtml = WebUtility.HtmlEncode(nombreTutor);
            var alumnoHtml = WebUtility.HtmlEncode(nombreAlumno);
            var instHtml  = WebUtility.HtmlEncode(nombreInstitucion);
            var anio      = DateTime.UtcNow.Year;

            var hasLogo   = logoBytes is { Length: > 0 };
            var logoBlock = hasLogo
                ? $"<div style=\"text-align:center;padding:12px 0 8px 0;\"><img src=\"cid:{LogoContentId}\" alt=\"Logo institucional\" style=\"height:58px;width:auto;display:inline-block;\" /></div>"
                : string.Empty;

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\" /></head>");
            sb.Append("<body style=\"margin:0;padding:14px 0 20px 0;background:#ffffff;font-family:Arial,Helvetica,sans-serif;color:#1f1f1f;font-style:italic;font-size:9pt;\">");
            sb.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\"><tr><td align=\"center\">");
            sb.Append("<table width=\"640\" cellpadding=\"0\" cellspacing=\"0\" role=\"presentation\" style=\"max-width:640px;background:#ffffff;padding:0 56px 14px 56px;\">");
            sb.Append("<tr><td>");

            sb.Append(logoBlock);
            sb.Append("<div style=\"border-top:1px solid #b5b5b5;margin:0 0 22px 0;\"></div>");

            sb.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Estimado/a <strong>{tutorHtml}</strong>:</p>");

            sb.Append($"<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Nos comunicamos desde el {instHtml} para informarle que los datos correspondientes a: <strong style=\"color:#1565c0;\">Teléfono de contacto, Domicilio y Disponibilidad horaria</strong> proporcionados al completarse el registro de tutor responsable del/la estudiante <strong>{alumnoHtml}</strong> se encuentran fuera de vigencia.</p>");

            sb.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Solicitamos que a partir de este aviso, responda este correo con la información solicitada actualizada, se comunique por llamada telefónica o se acerque presencialmente a la institución (Padre Luis Monti 1859, X5004ENI Córdoba) para realizar la actualización.</p>");

            sb.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Recordamos que esta información es de <strong>estricta importancia</strong> para nosotros ya que nos ayuda a seguir velando por el cuidado de nuestros estudiantes.</p>");

            sb.Append("<p style=\"margin:0 0 14px 0;font-size:9pt;line-height:1.38;\">Si considera que se trata de un error, desestime este correo.</p>");

            sb.Append("<p style=\"margin:0 0 22px 0;font-size:9pt;line-height:1.38;\">Muchas gracias por su compromiso.</p>");

            sb.Append("<p style=\"margin:0;font-size:9pt;line-height:1.34;\">Atentamente,</p>");
            sb.Append($"<p style=\"margin:0 0 2px 0;font-size:9pt;line-height:1.34;\">{instHtml}</p>");

            sb.Append("<div style=\"border-top:1px solid #b5b5b5;margin:16px 0 12px 0;\"></div>");
            sb.Append("<p style=\"margin:0;text-align:center;font-size:8pt;line-height:1.35;color:#6e6e6e;\">Secretaría de la institución Colegio Luis Manuel Robles, Padre Luis Monti 1859, X5004ENI Córdoba &ndash; 03514517213 &ndash; <u>colegiorobles.edu.ar</u></p>");
            sb.Append($"<p style=\"margin:10px 0 0 0;text-align:center;font-size:8pt;line-height:1.3;color:#8a8a8a;\">Desde &copy; PaletApp {anio}</p>");

            sb.Append("</td></tr></table>");
            sb.Append("</td></tr></table></body></html>");

            IEnumerable<EmailInlineResourceDto>? inlineResources = hasLogo
                ? new[]
                {
                    new EmailInlineResourceDto
                    {
                        ContentId  = LogoContentId,
                        ContentType = "image/png",
                        Content    = logoBytes!
                    }
                }
                : null;

            return (sb.ToString(), inlineResources);
        }
    }

}
