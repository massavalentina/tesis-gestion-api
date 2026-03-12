using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
using TesisGestorApi.Exceptions;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class ScannerService : IScannerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAsistenciaService _asistenciaService;

        public ScannerService(ApplicationDbContext context, IAsistenciaService asistenciaService)
        {
            _context = context;
            _asistenciaService = asistenciaService;
        }

        public async Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request)
        {
            if (request is null)
                throw new AsistenciaException("INVALID_REQUEST", "Solicitud invalida.");

            if (!Guid.TryParse(request.CodigoQr, out var qrGuid))
                throw new AsistenciaException("QR_INVALID", "Codigo QR invalido.");

            if (request.IdCurso == Guid.Empty)
                throw new AsistenciaException("COURSE_INVALID", "Curso invalido.");

            var turnoNormalizado = NormalizarTurno(request.Turno);

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

            var cursadoActivo = await _context.DetallesCursado
                .AsNoTracking()
                .Include(dc => dc.Curso)
                .Where(dc => dc.IdEstudiante == credencial.IdEstudiante && dc.Estado)
                .OrderByDescending(dc => dc.Curso.AñoLectivo)
                .FirstOrDefaultAsync();

            if (cursadoActivo is null)
                throw new AsistenciaException("STUDENT_INACTIVE", "Estudiante inactivo.");

            if (cursadoActivo.IdCurso != request.IdCurso)
                throw new AsistenciaException("STUDENT_NOT_IN_COURSE", "El estudiante no pertenece al curso seleccionado.");

            var nowLocal = DateTime.Now;
            var hoy = DateOnly.FromDateTime(nowLocal);

            var asistencia = await _context.Asistencias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EstudianteId == credencial.IdEstudiante && a.Fecha == hoy);

            if (asistencia is not null)
            {
                if (turnoNormalizado == "MANANA" && asistencia.TipoManianaId.HasValue)
                    throw new AsistenciaException("ALREADY_SCANNED", "Este alumno ya tiene asistencia cargada en el turno manana.");

                if (turnoNormalizado == "TARDE" && asistencia.TipoTardeId.HasValue)
                    throw new AsistenciaException("ALREADY_SCANNED", "Este alumno ya tiene asistencia cargada en el turno tarde.");
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
                    TipoAsistencia = "Pendiente de confirmar",
                    Turno = turnoNormalizado
                }
            };
        }

        public async Task ConfirmarAsync(ConfirmarAsistenciaRequest request)
        {
            if (request is null)
                throw new AsistenciaException("INVALID_REQUEST", "Solicitud invalida.");

            if (request.EstudianteIds is null || request.EstudianteIds.Count == 0)
                throw new AsistenciaException("EMPTY_STUDENTS", "No se recibieron estudiantes.");

            if (request.TipoAsistenciaId == Guid.Empty)
                throw new AsistenciaException("INVALID_ATTENDANCE_TYPE", "Tipo de asistencia invalido.");

            var tipoExiste = await _context.TiposAsistencia
                .AsNoTracking()
                .AnyAsync(t => t.IdTipo == request.TipoAsistenciaId);

            if (!tipoExiste)
                throw new AsistenciaException("INVALID_ATTENDANCE_TYPE", "Tipo de asistencia invalido.");

            var nowLocal = DateTime.Now;
            var hoy = DateOnly.FromDateTime(nowLocal);
            var hora = request.Hora ?? nowLocal.TimeOfDay;
            var turnoNormalizado = NormalizarTurno(request.Turno);

            var lista = request.EstudianteIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Select(id => new RegistrarAsistenciaDto
                {
                    EstudianteId = id,
                    Fecha = hoy,
                    Turno = turnoNormalizado,
                    TipoAsistenciaId = request.TipoAsistenciaId,
                    Hora = hora
                })
                .ToList();

            if (lista.Count == 0)
                throw new AsistenciaException("EMPTY_STUDENTS", "No se recibieron estudiantes validos.");

            await _asistenciaService.RegistrarLoteAsync(lista);
        }

        public Task<List<OpcionSeleccionDto>> ObtenerCursosScannerAsync()
        {
            var anioActual = DateTime.UtcNow.Year;

            return _context.Cursos
                .AsNoTracking()
                .Where(c => c.Estado && c.AñoLectivo.Year == anioActual)
                .OrderBy(c => c.Codigo)
                .Select(c => new OpcionSeleccionDto
                {
                    Id = c.IdCurso.ToString(),
                    Label = c.Codigo
                })
                .ToListAsync();
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

        public Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync()
        {
            return _context.TiposAsistencia
                .AsNoTracking()
                .Where(t => t.Codigo != "RE" && t.Codigo != "RAE")
                .OrderBy(t => t.Codigo)
                .Select(t => new OpcionSeleccionDto
                {
                    Id = t.IdTipo.ToString(),
                    Label = t.Descripcion
                })
                .ToListAsync();
        }

        private static string NormalizarTurno(string turno)
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
    }
}
