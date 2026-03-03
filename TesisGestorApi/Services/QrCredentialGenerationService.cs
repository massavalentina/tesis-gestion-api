using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class QrCredentialGenerationService : IQrCredentialGenerationService
    {
        private const string ScopeActivos = "ACTIVOS";
        private const string ScopeSinQr = "SIN_QR";
        private const string ScopeTodos = "TODOS";

        private readonly ApplicationDbContext _db;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly QrCredentialGenerationProgressStore _progress;

        public QrCredentialGenerationService(
            ApplicationDbContext db,
            IServiceScopeFactory scopeFactory,
            QrCredentialGenerationProgressStore progress)
        {
            _db = db;
            _scopeFactory = scopeFactory;
            _progress = progress;
        }

        public async Task<QrCredentialSummaryDto> GetSummaryAsync(Guid? cursoId, CancellationToken ct = default)
        {
            string? cursoCodigo = null;

            if (cursoId.HasValue && cursoId.Value != Guid.Empty)
            {
                var curso = await _db.Cursos
                    .AsNoTracking()
                    .Where(c => c.IdCurso == cursoId.Value)
                    .Select(c => new { c.IdCurso, c.Codigo, c.Estado })
                    .FirstOrDefaultAsync(ct);

                if (curso is null)
                    throw new InvalidOperationException("No existe el curso seleccionado.");

                if (!curso.Estado)
                    throw new InvalidOperationException("El curso seleccionado está inactivo.");

                cursoCodigo = curso.Codigo;
            }

            var query = _db.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.Estado)
                .Where(dc => !cursoId.HasValue || cursoId.Value == Guid.Empty || dc.IdCurso == cursoId.Value);

            var alumnosActivos = await query
                .Select(dc => dc.IdEstudiante)
                .Distinct()
                .ToListAsync(ct);

            if (alumnosActivos.Count == 0)
            {
                return new QrCredentialSummaryDto
                {
                    IdCurso = cursoId,
                    CursoCodigo = cursoCodigo,
                    TotalAlumnosActivos = 0,
                    TotalQrActivos = 0,
                    TotalPendientesGenerar = 0
                };
            }

            var estudiantesConQrActivo = await _db.CredencialesQR
                .AsNoTracking()
                .Where(c => c.Activo && alumnosActivos.Contains(c.IdEstudiante))
                .Select(c => c.IdEstudiante)
                .Distinct()
                .ToListAsync(ct);

            var totalQrActivos = estudiantesConQrActivo.Count;
            var totalPendientes = alumnosActivos.Count - totalQrActivos;

            return new QrCredentialSummaryDto
            {
                IdCurso = cursoId,
                CursoCodigo = cursoCodigo,
                TotalAlumnosActivos = alumnosActivos.Count,
                TotalQrActivos = totalQrActivos,
                TotalPendientesGenerar = totalPendientes
            };
        }

        public async Task<QrCredentialRegenerationResponseDto> RegenerateStudentCredentialAsync(Guid estudianteId, CancellationToken ct = default)
        {
            var estudiante = await _db.Estudiantes
                .AsNoTracking()
                .Where(e => e.IdEstudiante == estudianteId)
                .Select(e => new { e.IdEstudiante, e.Nombre, e.Apellido })
                .FirstOrDefaultAsync(ct);

            if (estudiante is null)
                throw new InvalidOperationException("No existe el estudiante seleccionado.");

            var cursoActivo = await _db.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdEstudiante == estudianteId && dc.Estado)
                .Join(
                    _db.Cursos.AsNoTracking().Where(c => c.Estado),
                    detalle => detalle.IdCurso,
                    curso => curso.IdCurso,
                    (detalle, curso) => new { curso.IdCurso, curso.AñoLectivo })
                .FirstOrDefaultAsync(ct);

            if (cursoActivo is null)
                throw new InvalidOperationException("El estudiante no tiene un curso activo asociado.");

            var credencialesActivas = await _db.CredencialesQR
                .Where(c => c.IdEstudiante == estudianteId && c.Activo)
                .ToListAsync(ct);

            foreach (var credencialActiva in credencialesActivas)
            {
                credencialActiva.Activo = false;
            }

            var ahora = DateTime.UtcNow;
            var nuevaCredencial = new CredencialQR
            {
                IdQR = Guid.NewGuid(),
                Codigo = Guid.NewGuid(),
                AñoLectivo = cursoActivo.AñoLectivo,
                Activo = true,
                Enviado = false,
                FechaGeneracion = ahora,
                FechaExpiracion = BuildExpirationDate(cursoActivo.AñoLectivo.Year),
                IdEstudiante = estudianteId
            };

            _db.CredencialesQR.Add(nuevaCredencial);
            _db.Entry(nuevaCredencial).Property("EstudianteIdEstudiante").CurrentValue = estudianteId;
            await _db.SaveChangesAsync(ct);

            return new QrCredentialRegenerationResponseDto
            {
                IdEstudiante = estudianteId,
                IdQr = nuevaCredencial.IdQR,
                CodigoQr = nuevaCredencial.Codigo,
                CredencialesDesactivadas = credencialesActivas.Count,
                Mensaje = credencialesActivas.Count > 0
                    ? "Se regeneró el QR y se desactivó la credencial anterior."
                    : "Se generó un nuevo QR para el estudiante."
            };
        }

        public async Task<QrCredentialStudentStatusDto> GetStudentCredentialStatusAsync(Guid estudianteId, CancellationToken ct = default)
        {
            var existeEstudiante = await _db.Estudiantes
                .AsNoTracking()
                .AnyAsync(e => e.IdEstudiante == estudianteId, ct);

            if (!existeEstudiante)
                throw new InvalidOperationException("No existe el estudiante seleccionado.");

            var credenciales = await _db.CredencialesQR
                .AsNoTracking()
                .Where(c => c.IdEstudiante == estudianteId)
                .OrderBy(c => c.FechaGeneracion)
                .ToListAsync(ct);

            if (credenciales.Count == 0)
            {
                return new QrCredentialStudentStatusDto
                {
                    IdEstudiante = estudianteId,
                    Estado = "NO_GENERADO",
                    VersionQr = 0,
                    FechaGeneracion = null
                };
            }

            var ultimaCredencial = credenciales[^1];

            return new QrCredentialStudentStatusDto
            {
                IdEstudiante = estudianteId,
                Estado = ultimaCredencial.Activo ? "ACTIVO" : "INACTIVO",
                VersionQr = credenciales.Count,
                FechaGeneracion = ultimaCredencial.FechaGeneracion
            };
        }

        public async Task<QrCredentialGenerationProgressDto> StartGenerationJobAsync(QrCredentialGenerationRequestDto req, CancellationToken ct = default)
        {
            var alcance = NormalizeScope(req.Alcance);

            var curso = await _db.Cursos
                .AsNoTracking()
                .Where(c => c.IdCurso == req.IdCurso)
                .Select(c => new { c.IdCurso, c.Codigo, c.Estado, c.AñoLectivo })
                .FirstOrDefaultAsync(ct);

            if (curso is null)
                throw new InvalidOperationException("No existe el curso seleccionado.");

            if (!curso.Estado)
                throw new InvalidOperationException("El curso seleccionado está inactivo.");

            var candidatos = await GetCandidateStudentIdsAsync(_db, req.IdCurso, alcance, ct);
            var job = _progress.Create(candidatos.Count);

            if (candidatos.Count == 0)
            {
                _progress.Update(job.JobId, p =>
                {
                    p.Estado = "COMPLETED";
                    p.Fin = DateTime.UtcNow;
                    p.UltimoMensaje = "No hay estudiantes para procesar con el alcance seleccionado.";
                });
                return job;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var estudiantes = await db.Estudiantes
                        .Where(e => candidatos.Contains(e.IdEstudiante))
                        .Select(e => new { e.IdEstudiante, e.Nombre, e.Apellido, e.Documento })
                        .ToListAsync(CancellationToken.None);

                    var estudiantesMap = estudiantes.ToDictionary(e => e.IdEstudiante);

                    foreach (var estudianteId in candidatos)
                    {
                        await WaitIfPausedAsync(job.JobId);

                        if (TryHandleCancellation(job.JobId, out var cancelMessage))
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Estado = "CANCELLED";
                                p.Fin = DateTime.UtcNow;
                                p.UltimoMensaje = cancelMessage;
                            });
                            return;
                        }

                        var estudiante = estudiantesMap[estudianteId];
                        var nombreCompleto = $"{estudiante.Apellido}, {estudiante.Nombre}";

                        try
                        {
                            var credencialesActivas = await db.CredencialesQR
                                .Where(c => c.IdEstudiante == estudianteId && c.Activo)
                                .ToListAsync(CancellationToken.None);

                            if (alcance == ScopeSinQr && credencialesActivas.Count > 0)
                            {
                                _progress.Update(job.JobId, p =>
                                {
                                    p.Procesados++;
                                    p.Omitidos++;
                                    p.UltimoEstudiante = nombreCompleto;
                                    p.UltimoMensaje = "Omitido: el estudiante ya tiene un QR activo.";
                                });
                                continue;
                            }

                            foreach (var credencialActiva in credencialesActivas)
                            {
                                credencialActiva.Activo = false;
                            }

                            var ahora = DateTime.UtcNow;
                            var nuevaCredencial = new CredencialQR
                            {
                                IdQR = Guid.NewGuid(),
                                Codigo = Guid.NewGuid(),
                                AñoLectivo = curso.AñoLectivo,
                                Activo = true,
                                Enviado = false,
                                FechaGeneracion = ahora,
                                FechaExpiracion = BuildExpirationDate(curso.AñoLectivo.Year),
                                IdEstudiante = estudianteId
                            };

                            db.CredencialesQR.Add(nuevaCredencial);
                            db.Entry(nuevaCredencial).Property("EstudianteIdEstudiante").CurrentValue = estudianteId;
                            await db.SaveChangesAsync(CancellationToken.None);
                            _progress.RecordGenerated(job.JobId, nuevaCredencial.IdQR);
                            _progress.RecordDeactivated(job.JobId, credencialesActivas.Select(c => c.IdQR));

                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Generados++;
                                p.Desactivados += credencialesActivas.Count;
                                p.UltimoEstudiante = nombreCompleto;
                                p.UltimoMensaje = credencialesActivas.Count > 0
                                    ? "Credencial regenerada y credencial previa desactivada."
                                    : "Credencial generada.";
                            });
                        }
                        catch (Exception ex)
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Procesados++;
                                p.Errores++;
                                p.UltimoEstudiante = nombreCompleto;
                                p.UltimoMensaje = ex.Message;
                            });
                        }
                    }

                    while (true)
                    {
                        await WaitIfPausedAsync(job.JobId);

                        if (TryHandleCancellation(job.JobId, out var cancelMessageFinal))
                        {
                            _progress.Update(job.JobId, p =>
                            {
                                p.Estado = "CANCELLED";
                                p.Fin = DateTime.UtcNow;
                                p.UltimoMensaje = cancelMessageFinal;
                            });
                            return;
                        }

                        if (_progress.TryMarkCompleted(job.JobId, out _))
                        {
                            return;
                        }
                    }
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

        public async Task<QrCredentialGenerationProgressDto> PauseGenerationJobAsync(Guid jobId, CancellationToken ct = default)
        {
            if (!_progress.TryGetState(jobId, out var state))
                throw new InvalidOperationException("No se encontró el proceso de generación indicado.");

            lock (state.SyncRoot)
            {
                if (state.Progress.Estado is "COMPLETED" or "FAILED" or "CANCELLED")
                    return CloneProgress(state.Progress);
            }

            _progress.RequestPause(jobId, out var progress);
            await Task.CompletedTask;
            return progress;
        }

        public async Task<QrCredentialGenerationProgressDto> ResumeGenerationJobAsync(Guid jobId, CancellationToken ct = default)
        {
            if (!_progress.TryGetState(jobId, out var state))
                throw new InvalidOperationException("No se encontró el proceso de generación indicado.");

            lock (state.SyncRoot)
            {
                if (state.Progress.Estado is "COMPLETED" or "FAILED" or "CANCELLED")
                    return CloneProgress(state.Progress);
            }

            _progress.Resume(jobId, out var progress);
            await Task.CompletedTask;
            return progress;
        }

        public async Task<QrCredentialGenerationProgressDto> CancelGenerationJobAsync(Guid jobId, bool mantenerGenerados, CancellationToken ct = default)
        {
            if (!_progress.TryGetState(jobId, out var state))
                throw new InvalidOperationException("No se encontró el proceso de generación indicado.");

            lock (state.SyncRoot)
            {
                if (state.Progress.Estado is "COMPLETED" or "FAILED" or "CANCELLED")
                    return CloneProgress(state.Progress);
            }

            _progress.RequestCancellation(jobId, mantenerGenerados, out var progress);
            await Task.CompletedTask;
            return progress;
        }

        private static string NormalizeScope(string? scope)
        {
            var normalized = (scope ?? ScopeActivos).Trim().ToUpperInvariant();
            return normalized switch
            {
                ScopeActivos => ScopeActivos,
                ScopeSinQr => ScopeSinQr,
                ScopeTodos => ScopeTodos,
                _ => throw new InvalidOperationException("Alcance inválido. Valores permitidos: ACTIVOS, SIN_QR, TODOS.")
            };
        }

        private static DateTime BuildExpirationDate(int anioLectivo)
        {
            return new DateTime(anioLectivo, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        }

        private static async Task<List<Guid>> GetCandidateStudentIdsAsync(
            ApplicationDbContext db,
            Guid cursoId,
            string alcance,
            CancellationToken ct)
        {
            var alumnosActivos = await db.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdCurso == cursoId && dc.Estado)
                .Select(dc => dc.IdEstudiante)
                .Distinct()
                .ToListAsync(ct);

            if (alcance == ScopeActivos || alcance == ScopeTodos)
                return alumnosActivos;

            var alumnosConQrActivo = await db.CredencialesQR
                .AsNoTracking()
                .Where(c => alumnosActivos.Contains(c.IdEstudiante) && c.Activo)
                .Select(c => c.IdEstudiante)
                .Distinct()
                .ToListAsync(ct);

            var alumnosConQrSet = alumnosConQrActivo.ToHashSet();
            return alumnosActivos
                .Where(id => !alumnosConQrSet.Contains(id))
                .ToList();
        }

        private bool TryHandleCancellation(Guid jobId, out string message)
        {
            message = string.Empty;

            if (!_progress.TryGetState(jobId, out var state))
                return false;

            bool cancelRequested;
            bool keepGenerated;

            lock (state.SyncRoot)
            {
                cancelRequested = state.CancellationRequested;
                keepGenerated = state.KeepGeneratedOnCancellation;
            }

            if (!cancelRequested)
                return false;

            if (!keepGenerated)
            {
                RevertGeneratedCredentials(jobId).GetAwaiter().GetResult();
                message = $"Proceso cancelado. Se revirtieron los cambios y no se conservaron los {state.Progress.Generados} QR(s) generados.";
                return true;
            }

            message = $"Proceso cancelado. Se conservaron {state.Progress.Generados} QR(s) generados de un total previsto de {state.Progress.Total}.";
            return true;
        }

        private async Task WaitIfPausedAsync(Guid jobId)
        {
            if (!_progress.TryGetState(jobId, out var state))
                return;

            Task<bool>? waitTask = null;

            lock (state.SyncRoot)
            {
                if (state.PauseRequested && state.Progress.Estado == "PAUSING")
                {
                    _progress.Pause(jobId, out _);
                }

                if (state.Progress.Estado == "PAUSED")
                {
                    waitTask = state.PauseReleaseSource?.Task;
                }
            }

            if (waitTask is not null)
            {
                await waitTask;
            }
        }

        private async Task RevertGeneratedCredentials(Guid jobId)
        {
            if (!_progress.TryGetState(jobId, out var state))
                return;

            List<Guid> generatedIds;
            List<Guid> deactivatedIds;

            lock (state.SyncRoot)
            {
                generatedIds = state.GeneratedCredentialIds.ToList();
                deactivatedIds = state.DeactivatedCredentialIds.ToList();
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (generatedIds.Count > 0)
            {
                var generated = await db.CredencialesQR
                    .Where(c => generatedIds.Contains(c.IdQR))
                    .ToListAsync(CancellationToken.None);

                db.CredencialesQR.RemoveRange(generated);
            }

            if (deactivatedIds.Count > 0)
            {
                var deactivated = await db.CredencialesQR
                    .Where(c => deactivatedIds.Contains(c.IdQR))
                    .ToListAsync(CancellationToken.None);

                foreach (var credential in deactivated)
                {
                    credential.Activo = true;
                }
            }

            await db.SaveChangesAsync(CancellationToken.None);
        }

        private static QrCredentialGenerationProgressDto CloneProgress(QrCredentialGenerationProgressDto progress)
        {
            return new QrCredentialGenerationProgressDto
            {
                JobId = progress.JobId,
                Estado = progress.Estado,
                Total = progress.Total,
                Procesados = progress.Procesados,
                Generados = progress.Generados,
                Desactivados = progress.Desactivados,
                Omitidos = progress.Omitidos,
                Errores = progress.Errores,
                UltimoEstudiante = progress.UltimoEstudiante,
                UltimoMensaje = progress.UltimoMensaje,
                Inicio = progress.Inicio,
                Fin = progress.Fin
            };
        }
    }
}
