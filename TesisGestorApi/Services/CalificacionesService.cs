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
        private readonly ICalificacionesWriteService _writeService;
        private readonly ISupabaseStorageService _storageService;

        public CalificacionesService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            ICalificacionesWriteService writeService,
            ISupabaseStorageService storageService)
        {
            _context = context;
            _currentUser = currentUser;
            _writeService = writeService;
            _storageService = storageService;
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
            var estudiantes = await LoadEstudiantesAsync(espacio.IdCurso, ct);
            var estudiantesById = estudiantes.ToDictionary(e => e.IdEstudiante);

            var normalizedChanges = NormalizeAndValidatePayload(dto.Cambios, instanciasById, estudiantesById);
            var result = await _writeService.ApplyChangesAsync(
                new CalificacionesApplyRequest(
                    idEC,
                    idUsuario,
                    docenteLabel,
                    OrigenCarga.Manual,
                    null,
                    normalizedChanges
                        .Select(change => new CalificacionApplyChange(
                            change.IdIE,
                            change.IdEstudiante,
                            change.TipoCalificacion,
                            change.Puntaje))
                        .ToList()),
                ct);

            return new GuardarCalificacionesManualResponseDto
            {
                CambiosAplicados = result.CambiosAplicados,
                InstanciasAfectadas = result.InstanciasAfectadas.ToList(),
                SesionAuditoria = result.SesionAuditoria,
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
                    RutaArchivoImportacion = s.IdImportacionCalificaciones.HasValue
                        && s.ImportacionCalificaciones != null
                        && !string.IsNullOrWhiteSpace(s.ImportacionCalificaciones.RutaArchivoFinal)
                            ? _storageService.GetUrlPublica(s.ImportacionCalificaciones.RutaArchivoFinal)
                            : null,
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
                            TipoCalificacion = CalificacionesDomainHelper.ToTipoCalificacionCode(d.TipoCalificacion),
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
                HasMore = skip + items.Count() < totalSesiones,
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
                    TipoCalificacion = CalificacionesDomainHelper.ToTipoCalificacionCode(c.TipoCalificacion),
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
                TipoCalificacion = CalificacionesDomainHelper.ToTipoCalificacionCode(archivo.TipoCalificacion),
                TipoIE = archivo.TipoIE.ToString(),
                Titulo = archivo.Titulo,
                FechaEjecucion = archivo.FechaEjecucion,
                FechaCarga = archivo.FechaCarga,
                NombreArchivo = archivo.NombreArchivo,
            };
        }

        private static bool TryParseTipoCalificacion(string rawValue, out TipoCalificacion tipoCalificacion)
        {
            return CalificacionesDomainHelper.TryParseTipoCalificacion(rawValue, out tipoCalificacion);
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

        private readonly record struct CalificacionKey(Guid IdIE, Guid IdEstudiante, TipoCalificacion TipoCalificacion);
    }
}
