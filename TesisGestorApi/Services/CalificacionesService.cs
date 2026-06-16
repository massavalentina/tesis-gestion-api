using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Calificaciones;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class CalificacionesService : ICalificacionesService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public CalificacionesService(ApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<InstanciaEvaluativaResumenDto>> GetInstanciasPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            await GetEspacioContextAsync(idEC, idDocente, ct);
            var instancias = await LoadInstanciasAsync(idEC, ct);
            ValidateInstancias(instancias);

            return instancias
                .OrderBy(i => i.Nro)
                .Select(MapInstanciaDto)
                .ToList();
        }

        public async Task<List<GestionManualEstudianteDto>> GetEstudiantesPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
            return await LoadEstudiantesAsync(espacio.IdCurso, ct);
        }

        public async Task<List<CalificacionVigenteDto>> GetCalificacionesVigentesPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            await GetEspacioContextAsync(idEC, idDocente, ct);
            var instancias = await LoadInstanciasAsync(idEC, ct);
            ValidateInstancias(instancias);

            var instanciaIds = instancias.Select(i => i.IdIE).ToList();
            return instanciaIds.Count == 0
                ? new List<CalificacionVigenteDto>()
                : await LoadCalificacionesVigentesDtoAsync(instanciaIds, ct);
        }

        public async Task<GuardarCalificacionesManualResponseDto> GuardarGestionManualAsync(Guid idEC, Guid idDocente, GuardarCalificacionesManualDto dto, CancellationToken ct)
        {
            if (dto?.Cambios == null || dto.Cambios.Count == 0)
            {
                throw new ValidationException("Se requiere al menos un cambio para guardar.");
            }

            var idUsuario = _currentUser.UserId ?? throw new UnauthorizedAccessException("Usuario no autenticado.");
            var docenteLabel = _currentUser.NombreCompleto;
            var espacio = await GetEspacioContextAsync(idEC, idDocente, ct);
            var instancias = await LoadInstanciasAsync(idEC, ct);
            ValidateInstancias(instancias);

            var instanciasById = instancias.ToDictionary(i => i.IdIE);
            var activeArchivoBySlot = BuildActiveArchivoDictionary(instancias);
            var estudiantes = await LoadEstudiantesAsync(espacio.IdCurso, ct);
            var estudiantesById = estudiantes.ToDictionary(e => e.IdEstudiante);

            var normalizedChanges = NormalizeAndValidatePayload(dto.Cambios, instanciasById, estudiantesById);
            var instanceIds = normalizedChanges.Select(c => c.IdIE).Distinct().ToList();
            var studentIds = normalizedChanges.Select(c => c.IdEstudiante).Distinct().ToList();

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

            foreach (var change in normalizedChanges)
            {
                var key = new CalificacionKey(change.IdIE, change.IdEstudiante, change.TipoCalificacion);
                calificacionesByKey.TryGetValue(key, out var vigente);

                var valorAnterior = vigente?.Puntaje;
                var valorNuevo = change.Puntaje;

                if (valorAnterior == valorNuevo)
                {
                    continue;
                }

                if (valorAnterior == null && valorNuevo == null)
                {
                    continue;
                }

                if (!activeArchivoBySlot.TryGetValue(new ArchivoSlotKey(change.IdIE, change.TipoCalificacion), out var archivo))
                {
                    throw new InvalidOperationException($"No existe un ArchivoIE activo para la instancia '{change.IdIE}' y el tipo '{ToTipoCalificacionCode(change.TipoCalificacion)}'.");
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
                    IdUsuarioCarga = idUsuario,
                    Origen = OrigenCarga.Manual,
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
                return new GuardarCalificacionesManualResponseDto
                {
                    CambiosAplicados = 0,
                };
            }

            AuditoriaCalificacionSesion? auditSession = null;

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                auditSession = new AuditoriaCalificacionSesion
                {
                    IdSesionAuditoria = Guid.NewGuid(),
                    IdEC = idEC,
                    IdUsuario = idUsuario,
                    Origen = OrigenCarga.Manual,
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

                var instanciasEvaluadas = await _context.Calificaciones
                    .AsNoTracking()
                    .Where(c => c.Habilitada && c.Puntaje != null && instanciasAfectadas.Contains(c.IdIE))
                    .Select(c => c.IdIE)
                    .Distinct()
                    .ToListAsync(ct);

                var instanciasTrackeadas = await _context.InstanciasEvaluativas
                    .Where(i => instanciasAfectadas.Contains(i.IdIE))
                    .ToListAsync(ct);

                var evaluadasSet = instanciasEvaluadas.ToHashSet();
                foreach (var instancia in instanciasTrackeadas)
                {
                    instancia.Estado = evaluadasSet.Contains(instancia.IdIE)
                        ? EstadoInstanciaEvaluativa.Evaluada
                        : EstadoInstanciaEvaluativa.Pendiente;
                    instancia.FechaModificacion = now;
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }

            return new GuardarCalificacionesManualResponseDto
            {
                CambiosAplicados = pendingAuditDetails.Count,
                InstanciasAfectadas = instanciasAfectadas.OrderBy(id => id).ToList(),
                SesionAuditoria = new AuditoriaCalificacionSesionDto
                {
                    IdSesionAuditoria = auditSession!.IdSesionAuditoria,
                    Timestamp = auditSession.FechaRegistro,
                    Docente = docenteLabel,
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
                        TipoCalificacion = ToTipoCalificacionCode(detail.TipoCalificacion),
                        ValorAnterior = detail.ValorAnterior,
                        ValorNuevo = detail.ValorNuevo,
                    }).ToList(),
                },
            };
        }

        public async Task<AuditoriaCalificacionesResponseDto> GetAuditoriaAsync(Guid idEC, Guid idDocente, int skip, int take, CancellationToken ct)
        {
            if (skip < 0)
            {
                throw new ValidationException("El parámetro 'skip' no puede ser negativo.");
            }

            if (take <= 0)
            {
                throw new ValidationException("El parámetro 'take' debe ser mayor a cero.");
            }

            await GetEspacioContextAsync(idEC, idDocente, ct);
            await EnsureInstanciaConsistencyAsync(idEC, ct);

            var totalSesiones = await _context.AuditoriasCalificacionesSesiones
                .AsNoTracking()
                .Where(s => s.IdEC == idEC)
                .CountAsync(ct);

            var items = await _context.AuditoriasCalificacionesSesiones
                .AsNoTracking()
                .Where(s => s.IdEC == idEC)
                .OrderByDescending(s => s.FechaRegistro)
                .Skip(skip)
                .Take(take)
                .Select(s => new AuditoriaCalificacionSesionDto
                {
                    IdSesionAuditoria = s.IdSesionAuditoria,
                    Timestamp = s.FechaRegistro,
                    Docente = $"{s.Usuario.Nombre} {s.Usuario.Apellido}",
                    Origen = s.Origen.ToString(),
                    CantidadCambios = s.Detalles.Count,
                    Cambios = s.Detalles
                        .OrderBy(d => d.IdDetalleAuditoria)
                        .Select(d => new AuditoriaCalificacionDetalleDto
                        {
                            IdDetalleAuditoria = d.IdDetalleAuditoria,
                            IdIE = d.IdIE,
                            IdEstudiante = d.IdEstudiante,
                            Estudiante = $"{d.Estudiante.Apellido}, {d.Estudiante.Nombre}",
                            Documento = d.Estudiante.Documento,
                            Evaluacion = $"Eval {d.InstanciaEvaluativa.Nro}",
                            TipoCalificacion = ToTipoCalificacionCode(d.TipoCalificacion),
                            ValorAnterior = d.ValorAnterior,
                            ValorNuevo = d.ValorNuevo,
                        })
                        .ToList(),
                })
                .ToListAsync(ct);

            return new AuditoriaCalificacionesResponseDto
            {
                Items = items,
                TotalSesiones = totalSesiones,
                HasMore = skip + items.Count < totalSesiones,
            };
        }

        private async Task<EspacioContext> GetEspacioContextAsync(Guid idEC, Guid idDocente, CancellationToken ct)
        {
            var espacio = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Where(ec => ec.IdEC == idEC)
                .Select(ec => new EspacioContext(
                    ec.IdEC,
                    ec.IdCurso,
                    ec.IdDocente,
                    ec.Curricula.Nombre,
                    ec.Curso.Codigo,
                    ec.Curso.Anio.Numero,
                    ec.Curso.Division.Nombre.ToString(),
                    ec.Curso.AñoLectivo.Year))
                .FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Espacio curricular no encontrado.");

            if (espacio.IdDocente != idDocente)
            {
                throw new UnauthorizedAccessException("No sos el docente titular de este espacio curricular.");
            }

            return espacio;
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

        private async Task EnsureInstanciaConsistencyAsync(Guid idEC, CancellationToken ct)
        {
            var instancias = await LoadInstanciasAsync(idEC, ct);
            ValidateInstancias(instancias);
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

        private static Dictionary<ArchivoSlotKey, ArchivoReadModel> BuildActiveArchivoDictionary(IEnumerable<InstanciaReadModel> instancias)
        {
            return instancias
                .SelectMany(i => i.Archivos.Select(a => new KeyValuePair<ArchivoSlotKey, ArchivoReadModel>(new ArchivoSlotKey(i.IdIE, a.TipoCalificacion), a)))
                .ToDictionary(item => item.Key, item => item.Value);
        }

        private static List<NormalizedChange> NormalizeAndValidatePayload(
            IEnumerable<GuardarCalificacionCambioDto> cambios,
            IReadOnlyDictionary<Guid, InstanciaReadModel> instanciasById,
            IReadOnlyDictionary<Guid, GestionManualEstudianteDto> estudiantesById)
        {
            var normalized = new List<NormalizedChange>();
            var duplicates = new HashSet<CalificacionKey>();

            foreach (var cambio in cambios)
            {
                if (cambio == null)
                {
                    throw new ValidationException("El payload contiene un cambio nulo.");
                }

                if (cambio.IdIE == Guid.Empty)
                {
                    throw new ValidationException("Cada cambio debe informar un idIE válido.");
                }

                if (cambio.IdEstudiante == Guid.Empty)
                {
                    throw new ValidationException("Cada cambio debe informar un idEstudiante válido.");
                }

                if (!instanciasById.ContainsKey(cambio.IdIE))
                {
                    throw new ValidationException($"La instancia evaluativa '{cambio.IdIE}' no pertenece al espacio curricular indicado.");
                }

                if (!estudiantesById.ContainsKey(cambio.IdEstudiante))
                {
                    throw new ValidationException($"El estudiante '{cambio.IdEstudiante}' no pertenece a la nómina oficial del curso.");
                }

                if (!TryParseTipoCalificacion(cambio.TipoCalificacion, out var tipoCalificacion))
                {
                    throw new ValidationException($"El tipo de calificación '{cambio.TipoCalificacion}' no es válido.");
                }

                if (cambio.Puntaje is < 1 or > 10)
                {
                    throw new ValidationException("Las notas manuales solo permiten enteros de 1 a 10 o null.");
                }

                var key = new CalificacionKey(cambio.IdIE, cambio.IdEstudiante, tipoCalificacion);
                if (!duplicates.Add(key))
                {
                    throw new ValidationException("El payload contiene cambios duplicados para la misma instancia, estudiante y tipo de calificación.");
                }

                normalized.Add(new NormalizedChange(cambio.IdIE, cambio.IdEstudiante, tipoCalificacion, cambio.Puntaje));
            }

            return normalized;
        }

        private async Task<List<CalificacionVigenteDto>> LoadCalificacionesVigentesDtoAsync(
            List<Guid> instanciaIds,
            CancellationToken ct)
        {
            var calificaciones = await _context.Calificaciones
                .AsNoTracking()
                .Where(c => c.Habilitada && instanciaIds.Contains(c.IdIE))
                .OrderBy(c => c.IdIE)
                .ThenBy(c => c.IdEstudiante)
                .ToListAsync(ct);

            if (calificaciones
                .GroupBy(c => new CalificacionKey(c.IdIE, c.IdEstudiante, c.TipoCalificacion))
                .Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException("Se detectaron múltiples calificaciones vigentes para la misma instancia, estudiante y tipo de calificación.");
            }

            return calificaciones
                .Select(c => new CalificacionVigenteDto
                {
                    IdCalificacion = c.IdCalificacion,
                    IdIE = c.IdIE,
                    IdEstudiante = c.IdEstudiante,
                    TipoCalificacion = ToTipoCalificacionCode(c.TipoCalificacion),
                    Puntaje = c.Puntaje,
                    FechaCarga = c.FechaCarga,
                    Origen = c.Origen.ToString(),
                })
                .ToList();
        }

        private static InstanciaEvaluativaResumenDto MapInstanciaDto(InstanciaReadModel instancia)
        {
            var archivos = instancia.Archivos.ToDictionary(a => a.TipoCalificacion);

            return new InstanciaEvaluativaResumenDto
            {
                IdIE = instancia.IdIE,
                IdEC = instancia.IdEC,
                Nro = instancia.Nro,
                Estado = instancia.Estado.ToString(),
                Archivos = new InstanciaEvaluativaArchivosDto
                {
                    NotaOriginal = archivos.TryGetValue(TipoCalificacion.NotaOriginal, out var notaOriginal)
                        ? MapArchivoDto(notaOriginal)
                        : null,
                    Recuperatorio1 = archivos.TryGetValue(TipoCalificacion.Recuperatorio1, out var recuperatorio1)
                        ? MapArchivoDto(recuperatorio1)
                        : null,
                    Recuperatorio2 = archivos.TryGetValue(TipoCalificacion.Recuperatorio2, out var recuperatorio2)
                        ? MapArchivoDto(recuperatorio2)
                        : null,
                },
            };
        }

        private static ArchivoIEResumenDto MapArchivoDto(ArchivoReadModel archivo)
        {
            return new ArchivoIEResumenDto
            {
                IdArchivoIE = archivo.IdArchivoIE,
                TipoCalificacion = ToTipoCalificacionCode(archivo.TipoCalificacion),
                TipoIE = archivo.TipoIE.ToString(),
                Titulo = archivo.Titulo,
                FechaEjecucion = archivo.FechaEjecucion,
                FechaCarga = archivo.FechaCarga,
                NombreArchivo = archivo.NombreArchivo,
            };
        }

        private static string ToTipoCalificacionCode(TipoCalificacion tipoCalificacion)
        {
            return tipoCalificacion switch
            {
                TipoCalificacion.NotaOriginal => "N",
                TipoCalificacion.Recuperatorio1 => "R1",
                TipoCalificacion.Recuperatorio2 => "R2",
                _ => tipoCalificacion.ToString(),
            };
        }

        private static bool TryParseTipoCalificacion(string rawValue, out TipoCalificacion tipoCalificacion)
        {
            tipoCalificacion = default;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var normalized = rawValue.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "N":
                case "NOTAORIGINAL":
                    tipoCalificacion = TipoCalificacion.NotaOriginal;
                    return true;
                case "R1":
                case "RECUPERATORIO1":
                    tipoCalificacion = TipoCalificacion.Recuperatorio1;
                    return true;
                case "R2":
                case "RECUPERATORIO2":
                    tipoCalificacion = TipoCalificacion.Recuperatorio2;
                    return true;
                default:
                    return false;
            }
        }

        private sealed record EspacioContext(
            Guid IdEC,
            Guid IdCurso,
            Guid? IdDocente,
            string NombreMateria,
            string CodigoCurso,
            int AnioNumero,
            string Division,
            int AnioLectivo);

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

        private sealed record NormalizedChange(
            Guid IdIE,
            Guid IdEstudiante,
            TipoCalificacion TipoCalificacion,
            int? Puntaje);

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
