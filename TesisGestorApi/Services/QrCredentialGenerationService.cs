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
    }
}
