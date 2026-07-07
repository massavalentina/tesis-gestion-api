using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Calificaciones;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class CalificacionesWriteService : ICalificacionesWriteService
    {
        private readonly ApplicationDbContext _context;

        public CalificacionesWriteService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CalificacionesApplyResult> ApplyChangesAsync(
            CalificacionesApplyRequest request,
            CancellationToken ct)
        {
            if (request.Cambios.Count == 0)
            {
                return new CalificacionesApplyResult(0, Array.Empty<Guid>(), null);
            }

            var instancias = await LoadInstanciasAsync(request.IdEC, ct);
            ValidateInstancias(instancias);

            var espacio = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Where(ec => ec.IdEC == request.IdEC)
                .Select(ec => new { ec.IdCurso })
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

            var estudiantes = await LoadEstudiantesAsync(espacio.IdCurso, ct);
            var instanciasById = instancias.ToDictionary(i => i.IdIE);
            var estudiantesById = estudiantes.ToDictionary(e => e.IdEstudiante);

            foreach (var cambio in request.Cambios)
            {
                if (!instanciasById.ContainsKey(cambio.IdIE))
                {
                    throw new InvalidOperationException($"La instancia evaluativa '{cambio.IdIE}' no pertenece al espacio curricular indicado.");
                }

                if (!estudiantesById.ContainsKey(cambio.IdEstudiante))
                {
                    throw new InvalidOperationException($"El estudiante '{cambio.IdEstudiante}' no pertenece al curso del espacio curricular.");
                }
            }

            var activeArchivoBySlot = BuildActiveArchivoDictionary(instancias);
            var instanceIds = request.Cambios.Select(c => c.IdIE).Distinct().ToList();
            var studentIds = request.Cambios.Select(c => c.IdEstudiante).Distinct().ToList();

            var calificacionesActuales = await _context.Calificaciones
                .Where(c => c.Habilitada && instanceIds.Contains(c.IdIE) && studentIds.Contains(c.IdEstudiante))
                .ToListAsync(ct);

            if (calificacionesActuales
                .GroupBy(c => new CalificacionKey(c.IdIE, c.IdEstudiante, c.TipoCalificacion))
                .Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException("Se detectaron múltiples calificaciones vigentes para la misma instancia, estudiante y tipo de calificación.");
            }

            var calificacionesByKey = calificacionesActuales.ToDictionary(
                c => new CalificacionKey(c.IdIE, c.IdEstudiante, c.TipoCalificacion));

            var pendingAuditDetails = new List<PendingAuditDetail>();
            var instanciasAfectadas = new HashSet<Guid>();
            var now = DateTime.UtcNow;

            foreach (var change in request.Cambios)
            {
                var key = new CalificacionKey(change.IdIE, change.IdEstudiante, change.TipoCalificacion);
                calificacionesByKey.TryGetValue(key, out var vigente);

                var valorAnterior = vigente?.Puntaje;
                var valorNuevo = change.Puntaje;

                if (valorAnterior == valorNuevo || (valorAnterior == null && valorNuevo == null))
                {
                    continue;
                }

                if (!activeArchivoBySlot.TryGetValue(new ArchivoSlotKey(change.IdIE, change.TipoCalificacion), out var archivo))
                {
                    throw new InvalidOperationException($"No existe un ArchivoIE activo para la instancia '{change.IdIE}' y el tipo '{CalificacionesDomainHelper.ToTipoCalificacionCode(change.TipoCalificacion)}'.");
                }

                if (vigente != null)
                {
                    vigente.Habilitada = false;
                }

                var nuevaCalificacion = new Calificacion
                {
                    IdCalificacion = Guid.NewGuid(),
                    IdIE = change.IdIE,
                    IdEstudiante = change.IdEstudiante,
                    TipoCalificacion = change.TipoCalificacion,
                    IdArchivoIE = archivo.IdArchivoIE,
                    Puntaje = valorNuevo,
                    Habilitada = true,
                    FechaCarga = now,
                    IdUsuarioCarga = request.IdUsuario,
                    Origen = request.Origen,
                    IdImportacionCalificaciones = request.IdImportacionCalificaciones,
                    IdCalificacionAnterior = vigente?.IdCalificacion,
                };

                _context.Calificaciones.Add(nuevaCalificacion);
                calificacionesByKey[key] = nuevaCalificacion;
                instanciasAfectadas.Add(change.IdIE);

                var estudiante = estudiantesById[change.IdEstudiante];
                var instancia = instanciasById[change.IdIE];
                pendingAuditDetails.Add(new PendingAuditDetail(
                    change.IdIE,
                    change.IdEstudiante,
                    change.TipoCalificacion,
                    valorAnterior,
                    valorNuevo,
                    vigente?.IdCalificacion,
                    nuevaCalificacion.IdCalificacion,
                    $"Eval {instancia.Nro}",
                    $"{estudiante.Apellido}, {estudiante.Nombre}",
                    estudiante.Documento));
            }

            if (pendingAuditDetails.Count == 0)
            {
                return new CalificacionesApplyResult(0, Array.Empty<Guid>(), null);
            }

            AuditoriaCalificacionSesion? auditSession = null;

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                auditSession = new AuditoriaCalificacionSesion
                {
                    IdSesionAuditoria = Guid.NewGuid(),
                    IdEC = request.IdEC,
                    IdUsuario = request.IdUsuario,
                    Origen = request.Origen,
                    IdImportacionCalificaciones = request.IdImportacionCalificaciones,
                    FechaRegistro = now,
                    Detalles = pendingAuditDetails.Select(detail => new AuditoriaCalificacionDetalle
                    {
                        IdDetalleAuditoria = Guid.NewGuid(),
                        IdIE = detail.IdIE,
                        IdEstudiante = detail.IdEstudiante,
                        TipoCalificacion = detail.TipoCalificacion,
                        ValorAnterior = detail.ValorAnterior,
                        ValorNuevo = detail.ValorNuevo,
                        IdCalificacionAnterior = detail.IdCalificacionAnterior,
                        IdCalificacionNueva = detail.IdCalificacionNueva,
                    }).ToList(),
                };

                _context.AuditoriasCalificacionesSesiones.Add(auditSession);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }

            var auditoria = new AuditoriaCalificacionSesionDto
            {
                IdSesionAuditoria = auditSession!.IdSesionAuditoria,
                Timestamp = auditSession.FechaRegistro,
                Docente = request.DocenteLabel,
                Origen = auditSession.Origen.ToString(),
                CantidadCambios = pendingAuditDetails.Count,
                Cambios = pendingAuditDetails.Select(detail => new AuditoriaCalificacionDetalleDto
                {
                    IdDetalleAuditoria = auditSession.Detalles.First(d => d.IdCalificacionNueva == detail.IdCalificacionNueva).IdDetalleAuditoria,
                    IdIE = detail.IdIE,
                    IdEstudiante = detail.IdEstudiante,
                    Estudiante = detail.Estudiante,
                    Documento = detail.Documento,
                    Evaluacion = detail.Evaluacion,
                    TipoCalificacion = CalificacionesDomainHelper.ToTipoCalificacionCode(detail.TipoCalificacion),
                    ValorAnterior = detail.ValorAnterior,
                    ValorNuevo = detail.ValorNuevo,
                }).ToList(),
            };

            return new CalificacionesApplyResult(
                pendingAuditDetails.Count,
                instanciasAfectadas.OrderBy(id => id).ToList(),
                auditoria);
        }

        private async Task<List<GestionManualEstudianteDto>> LoadEstudiantesAsync(Guid idCurso, CancellationToken ct)
        {
            return await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => dc.IdCurso == idCurso && dc.Estado)
                .OrderBy(dc => dc.Estudiante.Apellido)
                .ThenBy(dc => dc.Estudiante.Nombre)
                .Select(dc => new GestionManualEstudianteDto
                {
                    IdEstudiante = dc.IdEstudiante,
                    Nombre = dc.Estudiante.Nombre,
                    Apellido = dc.Estudiante.Apellido,
                    Documento = dc.Estudiante.Documento,
                })
                .ToListAsync(ct);
        }

        private async Task<List<InstanciaReadModel>> LoadInstanciasAsync(Guid idEC, CancellationToken ct)
        {
            return await _context.InstanciasEvaluativas
                .AsNoTracking()
                .Where(i => i.IdEC == idEC)
                .OrderBy(i => i.Nro)
                .Select(i => new InstanciaReadModel(
                    i.IdIE,
                    i.IdEC,
                    i.Nro,
                    i.Estado,
                    i.Archivos
                        .Where(a => a.Habilitada)
                        .Select(a => new ArchivoReadModel(
                            a.IdArchivoIE,
                            a.TipoCalificacion,
                            a.TipoIE,
                            a.Titulo,
                            a.FechaEjecucion,
                            a.FechaCarga,
                            a.NombreArchivo))
                        .ToList()))
                .ToListAsync(ct);
        }

        private static void ValidateInstancias(List<InstanciaReadModel> instancias)
        {
            if (instancias.Count > 8)
            {
                throw new InvalidOperationException("El espacio curricular tiene más de 8 instancias evaluativas registradas para el año lectivo.");
            }

            if (instancias.Any(i => i.Nro < 1 || i.Nro > 8))
            {
                throw new InvalidOperationException("Se detectaron instancias evaluativas con un número fuera del rango permitido 1..8.");
            }

            if (instancias.GroupBy(i => i.Nro).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException("Se detectaron instancias evaluativas duplicadas para el mismo número.");
            }

            if (instancias.Any(i => i.Archivos.GroupBy(a => a.TipoCalificacion).Any(group => group.Count() > 1)))
            {
                throw new InvalidOperationException("Se detectaron múltiples ArchivoIE activos para el mismo tipo de calificación en una instancia evaluativa.");
            }
        }

        private static Dictionary<ArchivoSlotKey, ArchivoReadModel> BuildActiveArchivoDictionary(IEnumerable<InstanciaReadModel> instancias)
        {
            return instancias
                .SelectMany(i => i.Archivos.Select(a => new KeyValuePair<ArchivoSlotKey, ArchivoReadModel>(new ArchivoSlotKey(i.IdIE, a.TipoCalificacion), a)))
                .ToDictionary(item => item.Key, item => item.Value);
        }

        private sealed record InstanciaReadModel(
            Guid IdIE,
            Guid IdEC,
            int Nro,
            EstadoInstanciaEvaluativa Estado,
            List<ArchivoReadModel> Archivos);

        private sealed record ArchivoReadModel(
            Guid IdArchivoIE,
            TipoCalificacion TipoCalificacion,
            TipoIE TipoIE,
            string Titulo,
            DateTime FechaEjecucion,
            DateTime FechaCarga,
            string NombreArchivo);

        private sealed record PendingAuditDetail(
            Guid IdIE,
            Guid IdEstudiante,
            TipoCalificacion TipoCalificacion,
            int? ValorAnterior,
            int? ValorNuevo,
            Guid? IdCalificacionAnterior,
            Guid IdCalificacionNueva,
            string Evaluacion,
            string Estudiante,
            string Documento);

        private readonly record struct CalificacionKey(Guid IdIE, Guid IdEstudiante, TipoCalificacion TipoCalificacion);
        private readonly record struct ArchivoSlotKey(Guid IdIE, TipoCalificacion TipoCalificacion);
    }
}
