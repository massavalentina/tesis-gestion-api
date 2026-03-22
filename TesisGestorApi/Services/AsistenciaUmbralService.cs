using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class AsistenciaUmbralService : IAsistenciaUmbralService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AsistenciaUmbralService> _logger;

        private static readonly int[] UMBRALES = new[] { 10, 15, 20, 25 };

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

                foreach (var u in UMBRALES)
                {
                    var notifExistente = await _db.AsistenciasUmbralNotificacion
                        .FirstOrDefaultAsync(n =>
                            n.IdEstudiante == estId &&
                            n.AnioLectivo == anioLectivo &&
                            n.Umbral == u, ct);

                    if (faltas >= u)
                    {
                        if (notifExistente == null)
                        {
                            var creada = await TryCrearNotificacionAsync(estId, anioLectivo, u, ahora, ct);
                            if (creada)
                            {
                                await EnviarUnEmailDeNotificacionAsync(estId, anioLectivo, u, ct);
                            }
                        }
                    }
                    else
                    {
                        if (notifExistente == null)
                            continue;

                        if (notifExistente.CantidadEnviados == 0)
                        {
                            _db.AsistenciasUmbralNotificacion.Remove(notifExistente);
                            await _db.SaveChangesAsync(ct);
                        }
                        else if (notifExistente.Estado == "PENDIENTE")
                        {
                            notifExistente.Estado = "COMPLETADO";
                            notifExistente.UltimoError = "Pendientes cancelados por corrección de inasistencias.";
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                }
            }
        }

        public async Task EnviarPendientesAsync(CancellationToken ct = default)
        {
            var ahora = DateTime.UtcNow;

            var pendientes = await _db.AsistenciasUmbralNotificacion
                .Where(n => n.Estado == "PENDIENTE")
                .Where(n => n.ProximoEnvioUtc <= ahora)
                .Where(n => n.CantidadEnviados < 3)
                .OrderBy(n => n.ProximoEnvioUtc)
                .Take(200)
                .ToListAsync(ct);

            foreach (var n in pendientes)
            {
                var resumen = await _db.AsistenciasResumenAnual
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.IdEstudiante == n.IdEstudiante &&
                        r.AnioLectivo == n.AnioLectivo, ct);

                var faltasActuales = resumen?.FaltasAcumuladas ?? 0m;

                if (faltasActuales < n.Umbral)
                {
                    if (n.CantidadEnviados == 0)
                    {
                        _db.AsistenciasUmbralNotificacion.Remove(n);
                    }
                    else
                    {
                        n.Estado = "COMPLETADO";
                        n.UltimoError = "Pendientes cancelados por corrección de inasistencias.";
                    }

                    await _db.SaveChangesAsync(ct);
                    continue;
                }

                await EnviarUnEmailDeNotificacionAsync(n.IdEstudiante, n.AnioLectivo, n.Umbral, ct);
            }
        }

        private async Task<bool> TryCrearNotificacionAsync(Guid estId, int anioLectivo, int umbral, DateTime ahora, CancellationToken ct)
        {
            var notif = new AsistenciaUmbralNotificacion
            {
                IdNotif = Guid.NewGuid(),
                IdEstudiante = estId,
                AnioLectivo = anioLectivo,
                Umbral = umbral,
                CantidadEnviados = 0,
                ProximoEnvioUtc = ahora,
                Estado = "PENDIENTE",
                CreadoUtc = ahora
            };

            _db.AsistenciasUmbralNotificacion.Add(notif);

            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                _db.Entry(notif).State = EntityState.Detached;
                return false;
            }
        }

        private async Task EnviarUnEmailDeNotificacionAsync(Guid estId, int anioLectivo, int umbral, CancellationToken ct)
        {
            var ahora = DateTime.UtcNow;

            var notif = await _db.AsistenciasUmbralNotificacion
                .FirstAsync(n => n.IdEstudiante == estId && n.AnioLectivo == anioLectivo && n.Umbral == umbral, ct);

            if (notif.Estado != "PENDIENTE")
                return;

            if (notif.CantidadEnviados >= 3)
            {
                notif.Estado = "COMPLETADO";
                await _db.SaveChangesAsync(ct);
                return;
            }

            if (notif.ProximoEnvioUtc > ahora)
                return;

            var emailTutor = await _db.Set<TutorEstudiante>()
                .Where(te => te.IdEstudiante == estId && te.EsPrincipal)
                .Select(te => te.Tutor.Correo)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(emailTutor))
            {
                emailTutor = await _db.Set<TutorEstudiante>()
                    .Where(te => te.IdEstudiante == estId)
                    .Select(te => te.Tutor.Correo)
                    .FirstOrDefaultAsync(ct);
            }

            if (string.IsNullOrWhiteSpace(emailTutor))
            {
                notif.UltimoError = "Tutor sin correo.";
                notif.ProximoEnvioUtc = ahora.AddDays(1);
                await _db.SaveChangesAsync(ct);
                return;
            }

            var alumno = await _db.Estudiantes.AsNoTracking()
                .FirstAsync(e => e.IdEstudiante == estId, ct);

            var nombreAlumno = $"{alumno.Apellido}, {alumno.Nombre}";

            var resumen = await _db.AsistenciasResumenAnual.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdEstudiante == estId && r.AnioLectivo == anioLectivo, ct);

            var faltasActuales = resumen?.FaltasAcumuladas ?? 0m;

            var (subject, body) = BuildEmailTemplate(umbral, nombreAlumno, anioLectivo, faltasActuales);

            try
            {
                await _emailSender.SendAsync(emailTutor, subject, body, ct);

                notif.CantidadEnviados++;
                notif.UltimoEnvioUtc = ahora;
                notif.UltimoError = null;

                if (notif.CantidadEnviados >= 3)
                {
                    notif.Estado = "COMPLETADO";
                }
                else
                {
                    notif.ProximoEnvioUtc = ahora.AddDays(1);
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación umbral {Umbral} a {Email}", umbral, emailTutor);
                notif.UltimoError = ex.Message;
                notif.ProximoEnvioUtc = ahora.AddDays(1);
                await _db.SaveChangesAsync(ct);
            }
        }

        private (string subject, string body) BuildEmailTemplate(int umbral, string alumno, int anioLectivo, decimal faltasActuales)
        {
            var subject = umbral switch
            {
                10 => $"Citación por inasistencias - {alumno} - 10 faltas ({anioLectivo})",
                15 => $"Aviso de inasistencias - {alumno} - 15 faltas ({anioLectivo})",
                20 => $"Citación por inasistencias - {alumno} - 20 faltas ({anioLectivo})",
                25 => $"Notificación TEA - {alumno} - 25 faltas ({anioLectivo})",
                _ => $"Notificación de asistencia - {alumno} ({anioLectivo})"
            };

            var accion = umbral switch
            {
                10 => "<b>Se cita al tutor</b> a concurrir al establecimiento para <b>firmar la notificación</b>.",
                15 => "Se informa el estado de inasistencias del estudiante para su conocimiento y seguimiento.",
                20 => "<b>Se cita al tutor</b> a concurrir al establecimiento para <b>firmar la notificación</b>.",
                25 => "<b>El estudiante pasa a condición TEA</b> (Trayectoria Escolar Asistida).",
                _ => "Notificación de asistencia."
            };

            var body = $@"
                <p>Hola,</p>
                <p>Se notifica que el estudiante <b>{alumno}</b> alcanzó el umbral de <b>{umbral} inasistencias</b> en el ciclo lectivo <b>{anioLectivo}</b>.</p>
                <p><b>Faltas acumuladas actuales:</b> {faltasActuales:0.##}</p>
                <p>{accion}</p>
                <p>Saludos.</p>";

            return (subject, body);
        }
    }
}