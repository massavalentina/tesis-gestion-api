using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
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
        // Envía un mail a todos los tutores principales del curso cuyos datos
        // llevan más de 6 meses sin actualizarse. Reutiliza IEmailSender.
        [HttpPost("curso/{idCurso:guid}/notificar-tutores-desactualizados")]
        public async Task<IActionResult> NotificarTutoresCurso(
            Guid idCurso,
            CancellationToken ct = default)
        {
            // Sin AsNoTracking para poder actualizar FechaUltimaNotificacion al enviar
            var estudiantes = await _db.Estudiantes
                .Where(e => e.DetallesCursado.Any(dc => dc.IdCurso == idCurso && dc.Estado))
                .Include(e => e.TutorEstudiantes)
                    .ThenInclude(te => te.Tutor)
                .ToListAsync(ct);

            var limite = DateTime.UtcNow.AddMonths(-6);
            var nombreInstitucion = _config["Institucion:Nombre"] ?? "la institución";

            int enviados = 0;
            int omitidos = 0;

            foreach (var estudiante in estudiantes)
            {
                // Buscar el tutor principal del estudiante
                var tutorLink = estudiante.TutorEstudiantes.FirstOrDefault(te => te.EsPrincipal);
                if (tutorLink == null) { omitidos++; continue; }

                var tutor = tutorLink.Tutor;

                // Omitir si los datos del tutor son recientes (< 6 meses)
                if (tutor.FechaUltimaActualizacion >= limite) { omitidos++; continue; }

                // Omitir si ya se envió una notificación en los últimos 6 meses
                if (tutor.FechaUltimaNotificacion.HasValue && tutor.FechaUltimaNotificacion >= limite)
                { omitidos++; continue; }

                var nombreAlumno = $"{estudiante.Nombre} {estudiante.Apellido}";
                var nombreTutor = $"{tutor.Nombre} {tutor.Apellido}";

                // Mismo cuerpo de mail que el endpoint individual
                var htmlBody = $@"<p>Estimado/a {nombreTutor},</p>
<p>Le informamos que los datos de contacto registrados para el/la estudiante
<strong>{nombreAlumno}</strong> en el <strong>{nombreInstitucion}</strong> no han sido actualizados
en los últimos 6 meses.</p>
<p>Le solicitamos que se comunique con la institución o concurra personalmente
para verificar y actualizar la siguiente información:</p>
<ul>
  <li>Teléfono de contacto</li>
  <li>Correo electrónico</li>
  <li>Disponibilidad horaria</li>
</ul>
<p>Si alguno de estos datos ha cambiado recientemente, le pedimos que nos lo
informe para mantener el registro actualizado. En caso de que toda la
información sea correcta, no es necesario que responda este mensaje.</p>
<p>Mantener estos datos actualizados es fundamental para garantizar una
comunicación fluida ante cualquier situación.</p>
<p>Muchas gracias.<br/><strong>{nombreInstitucion}</strong></p>";

                await _emailSender.SendAsync(
                    to: tutor.Correo,
                    subject: $"Actualización de datos de contacto – {nombreAlumno}",
                    htmlBody: htmlBody,
                    ct: ct);

                // Registrar la fecha de envío para limitar el reenvío
                tutor.FechaUltimaNotificacion = DateTime.UtcNow;
                enviados++;
            }

            // Persistir todas las fechas de notificación de una sola vez
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
        // Envía un mail al tutor principal si sus datos llevan más de 6 meses sin actualizarse.
        // Reutiliza el IEmailSender ya configurado en el proyecto.
        [HttpPost("estudiante/{idEstudiante:guid}/notificar-tutor-desactualizado")]
        public async Task<IActionResult> NotificarTutorDesactualizado(
            Guid idEstudiante,
            CancellationToken ct = default)
        {
            // Sin AsNoTracking para poder actualizar FechaUltimaNotificacion al enviar
            var estudiante = await _db.Estudiantes
                .Include(e => e.TutorEstudiantes)
                    .ThenInclude(te => te.Tutor)
                .FirstOrDefaultAsync(e => e.IdEstudiante == idEstudiante, ct);

            if (estudiante == null)
                return NotFound($"No se encontró el estudiante con ID {idEstudiante}.");

            // Buscar el tutor principal
            var tutorLink = estudiante.TutorEstudiantes.FirstOrDefault(te => te.EsPrincipal);
            if (tutorLink == null)
                return BadRequest(new NotificacionTutorResponseDto
                {
                    Enviado = false,
                    Mensaje = "El estudiante no tiene tutor principal asignado."
                });

            var tutor = tutorLink.Tutor;
            var limiteDesactualizacion = DateTime.UtcNow.AddMonths(-6);

            // Verificar que los datos del tutor estén desactualizados (> 6 meses)
            if (tutor.FechaUltimaActualizacion >= limiteDesactualizacion)
                return BadRequest(new NotificacionTutorResponseDto
                {
                    Enviado = false,
                    Mensaje = "El tutor principal fue actualizado recientemente. No es necesario enviar notificación."
                });

            // Verificar que no se haya enviado una notificación en los últimos 6 meses
            if (tutor.FechaUltimaNotificacion.HasValue && tutor.FechaUltimaNotificacion >= limiteDesactualizacion)
                return BadRequest(new NotificacionTutorResponseDto
                {
                    Enviado = false,
                    Mensaje = "Ya se envió una notificación de actualización en los últimos 6 meses."
                });

            // Leer el nombre de la institución desde la configuración; fallback genérico si no está definido
            var nombreInstitucion = _config["Institucion:Nombre"] ?? "la institución";

            var nombreAlumno = $"{estudiante.Nombre} {estudiante.Apellido}";
            var nombreTutor = $"{tutor.Nombre} {tutor.Apellido}";

            // Construir el cuerpo del mail en HTML conservando exactamente el texto solicitado
            var htmlBody = $@"<p>Estimado/a {nombreTutor},</p>
<p>Le informamos que los datos de contacto registrados para el/la estudiante
<strong>{nombreAlumno}</strong> en el <strong>{nombreInstitucion}</strong> no han sido actualizados
en los últimos 6 meses.</p>
<p>Le solicitamos que se comunique con la institución o concurra personalmente
para verificar y actualizar la siguiente información:</p>
<ul>
  <li>Teléfono de contacto</li>
  <li>Correo electrónico</li>
  <li>Disponibilidad horaria</li>
</ul>
<p>Si alguno de estos datos ha cambiado recientemente, le pedimos que nos lo
informe para mantener el registro actualizado. En caso de que toda la
información sea correcta, no es necesario que responda este mensaje.</p>
<p>Mantener estos datos actualizados es fundamental para garantizar una
comunicación fluida ante cualquier situación.</p>
<p>Muchas gracias.<br/><strong>{nombreInstitucion}</strong></p>";

            // Enviar el mail usando el servicio de SMTP ya configurado en el proyecto
            await _emailSender.SendAsync(
                to: tutor.Correo,
                subject: $"Actualización de datos de contacto – {nombreAlumno}",
                htmlBody: htmlBody,
                ct: ct);

            // Registrar la fecha de envío para limitar el reenvío a una vez cada 6 meses
            tutor.FechaUltimaNotificacion = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Ok(new NotificacionTutorResponseDto
            {
                Enviado = true,
                Mensaje = $"Notificación enviada a {tutor.Correo}."
            });
        }
    }

}
