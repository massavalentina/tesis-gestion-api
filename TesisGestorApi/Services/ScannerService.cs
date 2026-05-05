using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
using TesisGestorApi.Entities;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class ScannerService : IScannerService
    {
        private static readonly TimeSpan HoraCorteMananaTarde = new(13, 20, 0);

        private static readonly HashSet<string> CodigosPermitidosScanner = new(StringComparer.OrdinalIgnoreCase)
        {
            "P",
            "A",
            "LLT",
            "LLTE",
            "LLTC",
            "ANC"
        };

        private readonly ApplicationDbContext _context;
        private readonly IAsistenciaService _asistenciaService;

        public ScannerService(ApplicationDbContext context, IAsistenciaService asistenciaService)
        {
            _context = context;
            _asistenciaService = asistenciaService;
        }

        public TurnoSesionResponse ObtenerTurnoSesion(string? turno)
        {
            var (turnoSesion, nowLocal) = ResolverTurnoSesion(turno);

            return new TurnoSesionResponse
            {
                Turno = turnoSesion,
                ServerTime = nowLocal.ToString("HH:mm:ss"),
                CutoffTime = "13:20"
            };
        }

        public async Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request)
        {
            if (request is null)
                throw new AsistenciaException("INVALID_REQUEST", "Solicitud invalida.");

            if (!Guid.TryParse(request.CodigoQr, out var qrGuid))
                throw new AsistenciaException("QR_INVALID", "Codigo QR invalido.");

            var (turnoSesion, nowLocal) = ResolverTurnoSesion(request.Turno);

            var credencial = await _context.CredencialesQR
                .AsNoTracking()
                .Include(c => c.Estudiante)
                .FirstOrDefaultAsync(c => c.Codigo == qrGuid);

            if (credencial is null)
                throw new AsistenciaException("QR_INVALID", "Codigo no reconocido.");

            if (!credencial.Activo)
                throw new AsistenciaException("QR_INACTIVE", "QR inactivo.");

            if (credencial.FechaExpiracion < DateTime.UtcNow)
                throw new AsistenciaException("QR_EXPIRED", "QR expirado.");

            var cursadosActivos = await _context.DetallesCursado
                .AsNoTracking()
                .Include(dc => dc.Curso)
                .Where(dc => dc.IdEstudiante == credencial.IdEstudiante && dc.Estado)
                .OrderByDescending(dc => dc.Curso.AñoLectivo)
                .ToListAsync();

            if (cursadosActivos.Count == 0)
                throw new AsistenciaException("STUDENT_INACTIVE", "Estudiante inactivo.");

            if (cursadosActivos.Count > 1)
                throw new AsistenciaException("STUDENT_AMBIGUOUS_ENROLLMENT", "El estudiante posee más de un cursado activo.");

            var cursadoActivo = cursadosActivos[0];
            var hoy = DateOnly.FromDateTime(nowLocal);

            var asistencia = await _context.Asistencias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EstudianteId == credencial.IdEstudiante && a.Fecha == hoy);

            var yaRegistradoEnTurno = asistencia is not null && TieneAsistenciaEnTurno(asistencia, turnoSesion);
            var tipoAsistenciaActual = "Pendiente de confirmar";
            string? tipoAsistenciaCodigoActual = null;

            if (yaRegistradoEnTurno && asistencia is not null)
            {
                var tipoAsistenciaId = ObtenerTipoAsistenciaTurnoId(asistencia, turnoSesion);
                if (tipoAsistenciaId.HasValue)
                {
                    var tipoActual = await _context.TiposAsistencia
                        .AsNoTracking()
                        .Where(t => t.IdTipo == tipoAsistenciaId.Value)
                        .Select(t => new { t.Codigo, t.Descripcion })
                        .FirstOrDefaultAsync();

                    if (tipoActual is not null)
                    {
                        tipoAsistenciaActual = tipoActual.Descripcion;
                        tipoAsistenciaCodigoActual = tipoActual.Codigo.Trim().ToUpperInvariant();
                    }
                    else
                    {
                        tipoAsistenciaActual = "Asistencia registrada";
                    }
                }
            }

            return new PrevisualizarAsistenciaResponse
            {
                Estudiante = new EstudianteAsistenciaDto
                {
                    Id = credencial.Estudiante.IdEstudiante,
                    Nombre = credencial.Estudiante.Nombre,
                    Apellido = credencial.Estudiante.Apellido,
                    Curso = cursadoActivo.Curso.Codigo
                },
                Attendance = new AsistenciaEscaneoDto
                {
                    Hora = nowLocal.ToString("HH:mm:ss"),
                    TipoAsistencia = tipoAsistenciaActual,
                    TipoAsistenciaCodigo = tipoAsistenciaCodigoActual,
                    YaRegistradoEnTurno = yaRegistradoEnTurno,
                    Turno = turnoSesion
                }
            };
        }

        public async Task ConfirmarAsync(ConfirmarAsistenciaRequest request)
        {
            if (request is null)
                throw new AsistenciaException("INVALID_REQUEST", "Solicitud invalida.");

            var items = ConstruirItemsConfirmacion(request);
            if (items.Count == 0)
                throw new AsistenciaException("EMPTY_STUDENTS", "No se recibieron estudiantes.");

            var nowLocal = DateTime.Now;
            var hoy = DateOnly.FromDateTime(nowLocal);
            var hora = request.Hora ?? nowLocal.TimeOfDay;

            var normalizados = new List<ConfirmacionNormalizada>(items.Count);
            var errores = new List<ConfirmacionError>();

            foreach (var item in items)
            {
                if (item.EstudianteId == Guid.Empty)
                {
                    errores.Add(new ConfirmacionError(Guid.Empty, "INVALID_STUDENT", "Se recibió un estudiante inválido."));
                    continue;
                }

                if (item.TipoAsistenciaId == Guid.Empty)
                {
                    errores.Add(new ConfirmacionError(item.EstudianteId, "INVALID_ATTENDANCE_TYPE_SCANNER", "Tipo de asistencia inválido para scanner."));
                    continue;
                }

                try
                {
                    var turno = NormalizarTurno(item.Turno ?? request.Turno);
                    normalizados.Add(new ConfirmacionNormalizada(item.EstudianteId, item.TipoAsistenciaId, turno));
                }
                catch (AsistenciaException ex)
                {
                    errores.Add(new ConfirmacionError(item.EstudianteId, ex.Code, ex.Message));
                }
            }

            if (normalizados.Count == 0)
                throw new AsistenciaException("SCANNER_CONFIRM_VALIDATION_FAILED", "No hay registros válidos para confirmar.", errores);

            var tipoIds = normalizados.Select(x => x.TipoAsistenciaId).Distinct().ToList();
            var tiposById = await _context.TiposAsistencia
                .AsNoTracking()
                .Where(t => tipoIds.Contains(t.IdTipo))
                .ToDictionaryAsync(t => t.IdTipo);

            foreach (var item in normalizados)
            {
                if (!tiposById.TryGetValue(item.TipoAsistenciaId, out var tipoDb) || !EsTipoPermitidoScanner(tipoDb.Codigo))
                {
                    errores.Add(new ConfirmacionError(
                        item.EstudianteId,
                        "INVALID_ATTENDANCE_TYPE_SCANNER",
                        "El tipo de asistencia no está permitido en scanner."
                    ));
                }
            }

            var estudianteIds = normalizados.Select(x => x.EstudianteId).Distinct().ToList();

            var cantidadCursadosActivos = await _context.DetallesCursado
                .AsNoTracking()
                .Where(dc => estudianteIds.Contains(dc.IdEstudiante) && dc.Estado)
                .GroupBy(dc => dc.IdEstudiante)
                .Select(g => new { EstudianteId = g.Key, Cantidad = g.Count() })
                .ToDictionaryAsync(x => x.EstudianteId, x => x.Cantidad);

            foreach (var estudianteId in estudianteIds)
            {
                if (!cantidadCursadosActivos.TryGetValue(estudianteId, out var cantidad) || cantidad == 0)
                {
                    errores.Add(new ConfirmacionError(
                        estudianteId,
                        "STUDENT_INACTIVE",
                        "Estudiante inactivo."
                    ));
                    continue;
                }

                if (cantidad > 1)
                {
                    errores.Add(new ConfirmacionError(
                        estudianteId,
                        "STUDENT_AMBIGUOUS_ENROLLMENT",
                        "El estudiante posee más de un cursado activo."
                    ));
                }
            }

            if (errores.Count > 0)
            {
                throw new AsistenciaException(
                    "SCANNER_CONFIRM_VALIDATION_FAILED",
                    "Se detectaron conflictos al confirmar asistencias.",
                    errores
                );
            }

            var consolidado = normalizados
                .GroupBy(x => (x.EstudianteId, x.Turno))
                .Select(g => g.Last())
                .ToList();

            var lista = consolidado
                .Select(item => new RegistrarAsistenciaDto
                {
                    EstudianteId = item.EstudianteId,
                    Fecha = hoy,
                    Turno = item.Turno,
                    TipoAsistenciaId = item.TipoAsistenciaId,
                    Hora = hora
                })
                .ToList();

            await _asistenciaService.RegistrarLoteAsync(lista);
        }

        public List<OpcionSeleccionDto> ObtenerTurnos()
        {
            return
            [
                new OpcionSeleccionDto
                {
                    Id = "MANANA",
                    Label = "MANANA"
                },
                new OpcionSeleccionDto
                {
                    Id = "TARDE",
                    Label = "TARDE"
                }
            ];
        }

        public async Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync()
        {
            var codigosPermitidos = CodigosPermitidosScanner.ToArray();
            var orden = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["P"] = 1,
                ["A"] = 2,
                ["LLT"] = 3,
                ["LLTE"] = 4,
                ["LLTC"] = 5,
                ["ANC"] = 6
            };

            var tipos = await _context.TiposAsistencia
                .AsNoTracking()
                .Where(t => codigosPermitidos.Contains(t.Codigo))
                .Select(t => new { t.IdTipo, t.Descripcion, t.Codigo })
                .ToListAsync();

            return tipos
                .OrderBy(t => orden.TryGetValue(t.Codigo, out var idx) ? idx : int.MaxValue)
                .Select(t => new OpcionSeleccionDto
                {
                    Id = t.IdTipo.ToString(),
                    Label = t.Descripcion
                })
                .ToList();
        }

        private static List<ConfirmarAsistenciaItemRequest> ConstruirItemsConfirmacion(ConfirmarAsistenciaRequest request)
        {
            if (request.Items is { Count: > 0 })
                return request.Items;

            if (request.EstudianteIds is not { Count: > 0 })
                return [];

            return request.EstudianteIds
                .Select(id => new ConfirmarAsistenciaItemRequest
                {
                    EstudianteId = id,
                    TipoAsistenciaId = request.TipoAsistenciaId,
                    Turno = request.Turno
                })
                .ToList();
        }

        private static bool TieneAsistenciaEnTurno(Asistencia asistencia, string turno)
            => turno == "MANANA"
                ? asistencia.TipoManianaId.HasValue
                : asistencia.TipoTardeId.HasValue;

        private static Guid? ObtenerTipoAsistenciaTurnoId(Asistencia asistencia, string turno)
            => turno == "MANANA"
                ? asistencia.TipoManianaId
                : asistencia.TipoTardeId;

        private static bool EsTipoPermitidoScanner(string codigo)
            => CodigosPermitidosScanner.Contains(codigo.Trim().ToUpperInvariant());

        private static (string Turno, DateTime NowLocal) ResolverTurnoSesion(string? turno)
        {
            var nowLocal = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(turno))
                return (NormalizarTurno(turno), nowLocal);

            var turnoAuto = nowLocal.TimeOfDay < HoraCorteMananaTarde
                ? "MANANA"
                : "TARDE";

            return (turnoAuto, nowLocal);
        }

        private static string NormalizarTurno(string? turno)
        {
            if (string.IsNullOrWhiteSpace(turno))
                throw new AsistenciaException("INVALID_TURNO", "Turno invalido.");

            var t = turno.Trim().ToUpperInvariant();

            if (t is "MAÑANA" or "MANANA" or "M" or "AM")
                return "MANANA";

            if (t is "TARDE" or "T" or "PM")
                return "TARDE";

            throw new AsistenciaException("INVALID_TURNO", $"Turno invalido: {turno}");
        }

        private sealed record ConfirmacionNormalizada(Guid EstudianteId, Guid TipoAsistenciaId, string Turno);

        private sealed record ConfirmacionError(Guid StudentId, string Code, string Message);
    }
}
