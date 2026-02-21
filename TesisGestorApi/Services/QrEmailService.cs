using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace TesisGestorApi.Services
{
    public class QrEmailService : IQrEmailService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly QrEmailProgressStore _progress;
        private readonly IServiceScopeFactory _scopeFactory;

        private const int ANIO_LECTIVO_DEFAULT = 2026;

        public QrEmailService(ApplicationDbContext db, IEmailSender emailSender, QrEmailProgressStore progress, IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _emailSender = emailSender;
            _progress = progress;
            _scopeFactory = scopeFactory;
        }

        public async Task<QrEmailResumenDto> GetResumenAsync(QrEmailResumenRequestDto req, CancellationToken ct = default)
        {
            var anio = req.AnioLectivo ?? ANIO_LECTIVO_DEFAULT;

            var curso = await _db.Cursos
                .Where(c => c.IdCurso == req.IdCurso)
                .Select(c => new { c.IdCurso, c.Codigo, c.Estado })
                .FirstOrDefaultAsync(ct);

            if (curso is null)
            {
                return new QrEmailResumenDto
                {
                    AnioLectivo = anio,
                    IdCurso = req.IdCurso,
                    Mensaje = "No existe el curso seleccionado.",
                    PuedeIniciarEnvio = false
                };
            }

            if (!curso.Estado)
            {
                return new QrEmailResumenDto
                {
                    AnioLectivo = anio,
                    IdCurso = req.IdCurso,
                    CursoCodigo = curso.Codigo,
                    Mensaje = "El curso seleccionado está inactivo.",
                    PuedeIniciarEnvio = false
                };
            }

            // Alumnos activos en el curso
            var alumnosIds = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == req.IdCurso)
                .Where(dc => dc.Estado)
                .Select(dc => dc.IdEstudiante)
                .Distinct()
                .ToListAsync(ct);

            var totalAlumnos = alumnosIds.Count;

            // QRs activos del año lectivo
            var qrs = await _db.CredencialesQR
                .Where(q => alumnosIds.Contains(q.IdEstudiante))
                .Where(q => q.Activo)
                .Where(q => q.AñoLectivo.Year == anio)
                .Select(q => new { q.IdEstudiante, q.Enviado })
                .ToListAsync(ct);

            var qrPorAlumno = qrs
                .GroupBy(x => x.IdEstudiante)
                .ToDictionary(g => g.Key, g => g.First());

            int yaEnviados = 0;
            int conQrPendiente = 0;
            int sinQr = 0;

            foreach (var estId in alumnosIds)
            {
                if (!qrPorAlumno.TryGetValue(estId, out var qr))
                {
                    sinQr++;
                    continue;
                }

                if (qr.Enviado) yaEnviados++;
                else conQrPendiente++;
            }

            int candidatos = req.IncluirYaEnviados ? (conQrPendiente + yaEnviados) : conQrPendiente;
            int estimacionSeg = (int)Math.Ceiling(candidatos * 1.5);

            return new QrEmailResumenDto
            {
                AnioLectivo = anio,
                IdCurso = req.IdCurso,
                CursoCodigo = curso.Codigo,

                TotalAlumnosActivos = totalAlumnos,
                ConQrPendiente = conQrPendiente,
                YaEnviados = yaEnviados,
                SinQrGenerado = sinQr,
                EstimacionSegundos = estimacionSeg,
                PuedeIniciarEnvio = candidatos > 0,
                Mensaje = candidatos > 0
                    ? "Resumen generado correctamente."
                    : "No hay alumnos con QR para procesar según el alcance seleccionado."
            };
        }

        public async Task<QrEmailStartResponseDto> StartEnvioAsync(QrEmailStartRequestDto req, CancellationToken ct = default)
        {
            var resumen = await GetResumenAsync(new QrEmailResumenRequestDto
            {
                IdCurso = req.IdCurso,
                IncluirYaEnviados = req.IncluirYaEnviados,
                AnioLectivo = req.AnioLectivo
            }, ct);

            if (!resumen.PuedeIniciarEnvio)
            {
                return new QrEmailStartResponseDto
                {
                    AnioLectivo = resumen.AnioLectivo,
                    IdCurso = req.IdCurso,
                    TotalAlumnosActivos = resumen.TotalAlumnosActivos,
                    Mensaje = resumen.Mensaje
                };
            }

            var anio = resumen.AnioLectivo;

            // Alumnos activos
            var alumnosIds = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == req.IdCurso)
                .Where(dc => dc.Estado)
                .Select(dc => dc.IdEstudiante)
                .Distinct()
                .ToListAsync(ct);

            // QRs completos para actualizar Enviado
            var qrs = await _db.CredencialesQR
                .Where(q => alumnosIds.Contains(q.IdEstudiante))
                .Where(q => q.Activo)
                .Where(q => q.AñoLectivo.Year == anio)
                .ToListAsync(ct);

            var qrPorAlumno = qrs
                .GroupBy(q => q.IdEstudiante)
                .ToDictionary(g => g.Key, g => g.First());

            // Tutor principal + email (garantizado por tu regla)
            var tutores = await _db.Set<TutorEstudiante>()
                .Where(te => alumnosIds.Contains(te.IdEstudiante))
                .Where(te => te.EsPrincipal)
                .Select(te => new { te.IdEstudiante, Email = te.Tutor.Correo })
                .ToListAsync(ct);

            var emailPorAlumno = tutores
                .GroupBy(x => x.IdEstudiante)
                .ToDictionary(g => g.Key, g => g.First().Email);

            int procesados = 0, enviados = 0, omitidos = 0, errores = 0;
            var detOmitidos = new List<string>();
            var detErrores = new List<string>();

            foreach (var estId in alumnosIds)
            {
                // Si no hay QR, se omite (esto sí puede pasar)
                if (!qrPorAlumno.TryGetValue(estId, out var qr))
                {
                    omitidos++;
                    detOmitidos.Add($"Estudiante {estId}: sin QR generado.");
                    continue;
                }

                // Si no incluimos ya enviados, omitimos los que ya están enviados
                if (!req.IncluirYaEnviados && qr.Enviado)
                {
                    omitidos++;
                    detOmitidos.Add($"Estudiante {estId}: ya enviado (excluido por alcance).");
                    continue;
                }

                // Email garantizado (igual lo saco del diccionario)
                var email = emailPorAlumno[estId];

                procesados++;

                try
                {
                    var subject = $"Credencial QR - Año lectivo {anio}";
                    var body = $@"
                    <p>Hola,</p>
                    <p>Se envía la credencial QR correspondiente al año lectivo <b>{anio}</b>.</p>
                    <p>(El QR adjunto se incorporará en una siguiente etapa.)</p>
                    <p>Saludos.</p>";

                    await _emailSender.SendAsync(email, subject, body, ct);

                    qr.Enviado = true;
                    enviados++;
                }
                catch (Exception ex)
                {
                    errores++;
                    detErrores.Add($"Estudiante {estId}: error enviando a {email}. {ex.Message}");
                }
            }

            await _db.SaveChangesAsync(ct);

            return new QrEmailStartResponseDto
            {
                AnioLectivo = anio,
                IdCurso = req.IdCurso,
                TotalAlumnosActivos = resumen.TotalAlumnosActivos,
                Procesados = procesados,
                Enviados = enviados,
                Omitidos = omitidos,
                Errores = errores,
                DetallesOmitidos = detOmitidos,
                DetallesErrores = detErrores,
                Mensaje = errores == 0
                    ? "Envío finalizado."
                    : "Envío finalizado con errores. Revisar detalles."
            };
        }



        public async Task<QrEmailProgressDto> StartEnvioJobAsync(QrEmailStartRequestDto req, CancellationToken ct = default)
        {
            // Reusamos tu resumen para saber cuántos candidatos hay
            var resumen = await GetResumenAsync(new QrEmailResumenRequestDto
            {
                IdCurso = req.IdCurso,
                IncluirYaEnviados = req.IncluirYaEnviados,
                AnioLectivo = req.AnioLectivo
            }, ct);

            if (!resumen.PuedeIniciarEnvio)
            {
                // Job “vacío” (termina instantáneo)
                var empty = _progress.Create(0);
                _progress.Update(empty.JobId, p =>
                {
                    p.Estado = "COMPLETED";
                    p.Inicio = DateTime.UtcNow;
                    p.Fin = DateTime.UtcNow;
                    p.UltimoMensaje = resumen.Mensaje;
                });
                return empty;
            }

            var totalCandidatos = req.IncluirYaEnviados
                ? (resumen.ConQrPendiente + resumen.YaEnviados)
                : resumen.ConQrPendiente;

            var job = _progress.Create(totalCandidatos);

            // ✅ NO uses _db acá adentro. Creamos scope nuevo con db nuevo.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                    var anio = resumen.AnioLectivo;

                    // Alumnos activos
                    var alumnosIds = await db.DetallesCursado
                        .Where(dc => dc.IdCurso == req.IdCurso)
                        .Where(dc => dc.Estado)
                        .Select(dc => dc.IdEstudiante)
                        .Distinct()
                        .ToListAsync(CancellationToken.None);

                    // QRs completos para actualizar Enviado
                    var qrs = await db.CredencialesQR
                        .Where(q => alumnosIds.Contains(q.IdEstudiante))
                        .Where(q => q.Activo)
                        .Where(q => q.AñoLectivo.Year == anio)
                        .ToListAsync(CancellationToken.None);

                    var qrPorAlumno = qrs
                        .GroupBy(q => q.IdEstudiante)
                        .ToDictionary(g => g.Key, g => g.First());

                    // Tutor principal + email
                    var tutores = await db.Set<TutorEstudiante>()
                        .Where(te => alumnosIds.Contains(te.IdEstudiante))
                        .Where(te => te.EsPrincipal)
                        .Select(te => new { te.IdEstudiante, Email = te.Tutor.Correo })
                        .ToListAsync(CancellationToken.None);

                    var emailPorAlumno = tutores
                        .GroupBy(x => x.IdEstudiante)
                        .ToDictionary(g => g.Key, g => g.First().Email);

                    // Recorremos alumnos, actualizando progreso
                    foreach (var estId in alumnosIds)
                    {
                        // Si no hay QR -> omitido
                        if (!qrPorAlumno.TryGetValue(estId, out var qr))
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Omitidos++;
                                p.UltimoMensaje = "Omitido: sin QR generado.";
                            });
                            continue;
                        }

                        // Si no incluimos ya enviados, omitimos los enviados
                        if (!req.IncluirYaEnviados && qr.Enviado)
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Omitidos++;
                                p.UltimoMensaje = "Omitido: ya enviado (excluido por alcance).";
                            });
                            continue;
                        }

                        var email = emailPorAlumno[estId];

                        try
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.UltimoDestino = email;
                                p.UltimoMensaje = "Enviando…";
                            });

                            var subject = $"Credencial QR - Año lectivo {anio}";
                            var body = $@"
                        <p>Hola,</p>
                        <p>Se envía la credencial QR correspondiente al año lectivo <b>{anio}</b>.</p>
                        <p>(El QR adjunto se incorporará en una siguiente etapa.)</p>
                        <p>Saludos.</p>";

                            // ⚠️ Background job: NO uses ct del request (se muere cuando termina el request)
                            await emailSender.SendAsync(email, subject, body, CancellationToken.None);

                            // marcar como enviado
                            qr.Enviado = true;

                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Enviados++;
                                p.UltimoMensaje = "Enviado.";
                            });
                        }
                        catch (Exception ex)
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Errores++;
                                p.UltimoMensaje = $"Error: {ex.Message}";
                            });
                        }
                    }

                    await db.SaveChangesAsync(CancellationToken.None);

                    _progress.Update(job.JobId, p =>
                    {
                        p.Estado = "COMPLETED";
                        p.Fin = DateTime.UtcNow;
                        p.UltimoMensaje = "Proceso finalizado.";
                    });
                }
                catch (Exception ex)
                {
                    _progress.Update(job.JobId, p =>
                    {
                        p.Estado = "FAILED";
                        p.Fin = DateTime.UtcNow;
                        p.UltimoMensaje = ex.Message;
                    });
                }
            });

            return job;
        }

        public async Task<List<QrAlumnoEstadoDto>> GetAlumnosEstadoAsync(Guid? cursoId, string? estado, int anioLectivo, CancellationToken ct = default)
        {
            var query = _db.DetallesCursado
                .Where(dc => dc.Estado)
                .AsQueryable();

            if (cursoId.HasValue && cursoId.Value != Guid.Empty)
                query = query.Where(dc => dc.IdCurso == cursoId.Value);

            var alumnos = await query
                .Select(dc => dc.Estudiante)
                .Distinct()
                .Select(e => new
                {
                    e.IdEstudiante,
                    Nombre = e.Nombre,
                    Apellido = e.Apellido,
                    Dni = e.Documento
                })
                .ToListAsync(ct);

            var ids = alumnos.Select(a => a.IdEstudiante).ToList();

            var qrs = await _db.CredencialesQR
                .Where(q => ids.Contains(q.IdEstudiante))
                .Where(q => q.Activo)
                .Where(q => q.AñoLectivo.Year == anioLectivo)
                .Select(q => new { q.IdEstudiante, q.Enviado })
                .ToListAsync(ct);

            var qrMap = qrs
                .GroupBy(x => x.IdEstudiante)
                .ToDictionary(g => g.Key, g => g.First().Enviado);

            string ComputeEstado(Guid id)
            {
                if (!qrMap.ContainsKey(id)) return "NO_GENERADO";
                return qrMap[id] ? "ENVIADO" : "PENDIENTE_ENVIO";
            }

            var rows = alumnos.Select(a => new QrAlumnoEstadoDto
            {
                IdEstudiante = a.IdEstudiante,
                NombreCompleto = $"{a.Apellido}, {a.Nombre}",
                Dni = a.Dni,
                Estado = ComputeEstado(a.IdEstudiante)
            }).ToList();

            if (!string.IsNullOrWhiteSpace(estado))
                rows = rows.Where(r => r.Estado == estado).ToList();

            return rows;
        }
    }
}