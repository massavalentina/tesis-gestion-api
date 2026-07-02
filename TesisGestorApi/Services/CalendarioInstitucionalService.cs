using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Calendario;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class CalendarioInstitucionalService : ICalendarioInstitucionalService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<CalendarioInstitucionalService> _logger;

        private static readonly Dictionary<TipoEventoInstitucional, string> TipoLabels = new()
        {
            { TipoEventoInstitucional.EventoInstitucional, "Evento Institucional" },
            { TipoEventoInstitucional.EventoExtraordinario, "Evento Extraordinario" },
            { TipoEventoInstitucional.EventoFestivo, "Evento Festivo" },
            { TipoEventoInstitucional.Feriado, "Feriado" },
            { TipoEventoInstitucional.PeriodoDeClases, "Período de Clases" },
            { TipoEventoInstitucional.PeriodoDeEvaluacion, "Período de Evaluación" },
        };

        public CalendarioInstitucionalService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            ILogger<CalendarioInstitucionalService> logger)
        {
            _context = context;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task<List<EventoInstitucionalDto>> ObtenerEventosAsync(
            int anioLectivo, Guid? idUsuario, List<string> roles, bool esAdmin, CancellationToken ct)
        {
            var eventos = await _context.EventosInstitucionales
                .AsNoTracking()
                .Where(e => e.Activo && e.AnioLectivo == anioLectivo)
                .Include(e => e.Cursos)
                    .ThenInclude(ec => ec.Curso)
                        .ThenInclude(c => c.Anio)
                .Include(e => e.Cursos)
                    .ThenInclude(ec => ec.Curso)
                        .ThenInclude(c => c.Division)
                .OrderBy(e => e.FechaInicio)
                .ToListAsync(ct);

            // Filtrar por rol: docente y preceptor solo ven eventos generales + de sus cursos
            if (!esAdmin
                && !roles.Contains("Equipo Directivo")
                && !roles.Contains("Secretario")
                && idUsuario.HasValue)
            {
                var cursoIds = await ObtenerCursoIdsUsuarioAsync(idUsuario.Value, roles, ct);
                eventos = eventos.Where(e =>
                    e.Cursos.Count == 0 // evento general (aplica a todos)
                    || e.Cursos.Any(c => cursoIds.Contains(c.IdCurso))
                ).ToList();
            }

            return eventos.Select(MapToDto).ToList();
        }

        public async Task<List<object>> ObtenerCursosUsuarioAsync(
            int anioLectivo, Guid? idUsuario, List<string> roles, bool esAdmin, CancellationToken ct)
        {
            var query = _context.Cursos
                .AsNoTracking()
                .Where(c => c.Estado && c.AñoLectivo.Year == anioLectivo);

            // Filtrar cursos por rol
            if (!esAdmin
                && !roles.Contains("Equipo Directivo")
                && !roles.Contains("Secretario")
                && idUsuario.HasValue)
            {
                var cursoIds = await ObtenerCursoIdsUsuarioAsync(idUsuario.Value, roles, ct);
                query = query.Where(c => cursoIds.Contains(c.IdCurso));
            }

            return await query
                .OrderBy(c => c.Anio.Numero)
                .ThenBy(c => c.Division.Nombre)
                .Select(c => (object)new
                {
                    id = c.IdCurso,
                    label = $"{c.Anio.Numero}°{c.Division.Nombre}",
                })
                .ToListAsync(ct);
        }

        /// Obtiene los IDs de curso asociados al usuario según su rol (Docente o Preceptor)
        private async Task<List<Guid>> ObtenerCursoIdsUsuarioAsync(
            Guid idUsuario, List<string> roles, CancellationToken ct)
        {
            var cursoIds = new HashSet<Guid>();

            if (roles.Contains("Docente"))
            {
                var docente = await _context.Docentes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);

                if (docente != null)
                {
                    var ids = await _context.EspaciosCurriculares
                        .AsNoTracking()
                        .Where(ec => ec.IdDocente == docente.IdDocente)
                        .Select(ec => ec.IdCurso)
                        .Distinct()
                        .ToListAsync(ct);
                    foreach (var id in ids) cursoIds.Add(id);
                }
            }

            if (roles.Contains("Preceptor"))
            {
                var preceptor = await _context.Preceptores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdUsuario == idUsuario, ct);

                if (preceptor != null)
                {
                    var ids = await _context.Cursos
                        .AsNoTracking()
                        .Where(c => c.IdPreceptor == preceptor.IdPreceptor)
                        .Select(c => c.IdCurso)
                        .ToListAsync(ct);
                    foreach (var id in ids) cursoIds.Add(id);
                }
            }

            return cursoIds.ToList();
        }

        public async Task<EventoInstitucionalDto> ObtenerPorIdAsync(Guid idEvento, CancellationToken ct)
        {
            var evento = await _context.EventosInstitucionales
                .AsNoTracking()
                .Include(e => e.Cursos)
                    .ThenInclude(ec => ec.Curso)
                        .ThenInclude(c => c.Anio)
                .Include(e => e.Cursos)
                    .ThenInclude(ec => ec.Curso)
                        .ThenInclude(c => c.Division)
                .FirstOrDefaultAsync(e => e.IdEvento == idEvento && e.Activo, ct)
                ?? throw new KeyNotFoundException($"No se encontró el evento con id '{idEvento}'.");

            return MapToDto(evento);
        }

        public async Task<EventoInstitucionalDto> CrearEventoAsync(
            CrearEventoInstitucionalDto dto, CancellationToken ct)
        {
            var tipo = (TipoEventoInstitucional)dto.TipoEvento;
            var fechaInicio = DateOnly.ParseExact(dto.FechaInicio, "yyyy-MM-dd");
            var fechaFin = DateOnly.ParseExact(dto.FechaFin, "yyyy-MM-dd");

            ValidarFechas(fechaInicio, fechaFin);
            var anioLectivo = fechaInicio.Year;

            AjustarPorTipo(tipo, ref dto);
            ValidarCambioActividad(dto);

            if (tipo == TipoEventoInstitucional.PeriodoDeClases)
                await ValidarSuperposicionPeriodoClasesAsync(fechaInicio, fechaFin, anioLectivo, null, ct);

            var evento = new EventoInstitucional
            {
                IdEvento = Guid.NewGuid(),
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                TipoEvento = tipo,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                AnioLectivo = anioLectivo,
                ContabilizaAsistencia = dto.ContabilizaAsistencia,
                CambioActividad = dto.CambioActividad,
                ComentarioCambioActividad = dto.CambioActividad ? dto.ComentarioCambioActividad : null,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                IdUsuarioCreacion = _currentUser.UserId
                    ?? throw new UnauthorizedAccessException("No se pudo determinar el usuario actual."),
            };

            if (tipo != TipoEventoInstitucional.PeriodoDeClases && dto.CursoIds is { Count: > 0 })
            {
                foreach (var cursoId in dto.CursoIds)
                {
                    evento.Cursos.Add(new EventoInstitucionalCurso
                    {
                        IdEvento = evento.IdEvento,
                        IdCurso = cursoId,
                    });
                }
            }

            _context.EventosInstitucionales.Add(evento);

            var auditoria = CrearAuditoria(evento.IdEvento, TipoOperacionEvento.Creacion,
                null, SerializarEvento(evento));
            _context.AuditoriasEventosInstitucionales.Add(auditoria);

            await _context.SaveChangesAsync(ct);

            return await ObtenerPorIdAsync(evento.IdEvento, ct);
        }

        public async Task<EventoInstitucionalDto> ActualizarEventoAsync(
            Guid idEvento, ActualizarEventoInstitucionalDto dto, CancellationToken ct)
        {
            var evento = await _context.EventosInstitucionales
                .Include(e => e.Cursos)
                .FirstOrDefaultAsync(e => e.IdEvento == idEvento && e.Activo, ct)
                ?? throw new KeyNotFoundException($"No se encontró el evento con id '{idEvento}'.");

            var tipo = (TipoEventoInstitucional)dto.TipoEvento;
            var fechaInicio = DateOnly.ParseExact(dto.FechaInicio, "yyyy-MM-dd");
            var fechaFin = DateOnly.ParseExact(dto.FechaFin, "yyyy-MM-dd");

            ValidarFechas(fechaInicio, fechaFin);
            var anioLectivo = fechaInicio.Year;

            var crearDto = new CrearEventoInstitucionalDto
            {
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                TipoEvento = dto.TipoEvento,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                ContabilizaAsistencia = dto.ContabilizaAsistencia,
                CambioActividad = dto.CambioActividad,
                ComentarioCambioActividad = dto.ComentarioCambioActividad,
                CursoIds = dto.CursoIds,
            };

            AjustarPorTipo(tipo, ref crearDto);
            ValidarCambioActividad(crearDto);

            if (tipo == TipoEventoInstitucional.PeriodoDeClases)
                await ValidarSuperposicionPeriodoClasesAsync(fechaInicio, fechaFin, anioLectivo, idEvento, ct);

            var valoresAnteriores = SerializarEvento(evento);

            evento.Titulo = crearDto.Titulo;
            evento.Descripcion = crearDto.Descripcion;
            evento.TipoEvento = tipo;
            evento.FechaInicio = fechaInicio;
            evento.FechaFin = fechaFin;
            evento.AnioLectivo = anioLectivo;
            evento.ContabilizaAsistencia = crearDto.ContabilizaAsistencia;
            evento.CambioActividad = crearDto.CambioActividad;
            evento.ComentarioCambioActividad = crearDto.CambioActividad ? crearDto.ComentarioCambioActividad : null;
            evento.FechaModificacion = DateTime.UtcNow;

            // Actualizar cursos
            evento.Cursos.Clear();
            if (tipo != TipoEventoInstitucional.PeriodoDeClases && crearDto.CursoIds is { Count: > 0 })
            {
                foreach (var cursoId in crearDto.CursoIds)
                {
                    evento.Cursos.Add(new EventoInstitucionalCurso
                    {
                        IdEvento = evento.IdEvento,
                        IdCurso = cursoId,
                    });
                }
            }

            var auditoria = CrearAuditoria(evento.IdEvento, TipoOperacionEvento.Modificacion,
                valoresAnteriores, SerializarEvento(evento));
            _context.AuditoriasEventosInstitucionales.Add(auditoria);

            await _context.SaveChangesAsync(ct);

            return await ObtenerPorIdAsync(evento.IdEvento, ct);
        }

        public async Task EliminarEventoAsync(Guid idEvento, CancellationToken ct)
        {
            var evento = await _context.EventosInstitucionales
                .FirstOrDefaultAsync(e => e.IdEvento == idEvento && e.Activo, ct)
                ?? throw new KeyNotFoundException($"No se encontró el evento con id '{idEvento}'.");

            var valoresAnteriores = SerializarEvento(evento);

            evento.Activo = false;
            evento.FechaModificacion = DateTime.UtcNow;

            var auditoria = CrearAuditoria(evento.IdEvento, TipoOperacionEvento.Eliminacion,
                valoresAnteriores, null);
            _context.AuditoriasEventosInstitucionales.Add(auditoria);

            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<AuditoriaEventoDto>> ObtenerAuditoriaEventoAsync(
            Guid idEvento, CancellationToken ct)
        {
            return await _context.AuditoriasEventosInstitucionales
                .AsNoTracking()
                .Where(a => a.IdEvento == idEvento)
                .Include(a => a.Usuario)
                .OrderByDescending(a => a.FechaRegistro)
                .Select(a => new AuditoriaEventoDto
                {
                    IdAuditoria = a.IdAuditoria,
                    TipoOperacion = a.TipoOperacion == TipoOperacionEvento.Creacion ? "Creación"
                        : a.TipoOperacion == TipoOperacionEvento.Modificacion ? "Modificación"
                        : "Eliminación",
                    ValoresAnteriores = a.ValoresAnteriores,
                    ValoresNuevos = a.ValoresNuevos,
                    NombreUsuario = a.Usuario.Nombre,
                    ApellidoUsuario = a.Usuario.Apellido,
                    FechaRegistro = a.FechaRegistro,
                })
                .ToListAsync(ct);
        }

        public async Task<List<AuditoriaEventoDto>> ObtenerAuditoriaGeneralAsync(
            int anioLectivo, CancellationToken ct)
        {
            return await _context.AuditoriasEventosInstitucionales
                .AsNoTracking()
                .Where(a => a.EventoInstitucional.AnioLectivo == anioLectivo)
                .Include(a => a.Usuario)
                .OrderByDescending(a => a.FechaRegistro)
                .Take(100)
                .Select(a => new AuditoriaEventoDto
                {
                    IdAuditoria = a.IdAuditoria,
                    TipoOperacion = a.TipoOperacion == TipoOperacionEvento.Creacion ? "Creación"
                        : a.TipoOperacion == TipoOperacionEvento.Modificacion ? "Modificación"
                        : "Eliminación",
                    ValoresAnteriores = a.ValoresAnteriores,
                    ValoresNuevos = a.ValoresNuevos,
                    NombreUsuario = a.Usuario.Nombre,
                    ApellidoUsuario = a.Usuario.Apellido,
                    FechaRegistro = a.FechaRegistro,
                })
                .ToListAsync(ct);
        }

        // ─── Helpers privados ────────────────────────────────────────────────────────

        private static void ValidarFechas(DateOnly inicio, DateOnly fin)
        {
            if (inicio > fin)
                throw new InvalidOperationException("La fecha de inicio no puede ser posterior a la fecha de fin.");

            if (inicio.Year != fin.Year)
                throw new InvalidOperationException("Las fechas deben pertenecer al mismo año lectivo.");
        }

        private static void AjustarPorTipo(TipoEventoInstitucional tipo, ref CrearEventoInstitucionalDto dto)
        {
            if (tipo == TipoEventoInstitucional.PeriodoDeClases)
            {
                dto.ContabilizaAsistencia = true;
                dto.CursoIds = null;
            }
            else if (tipo == TipoEventoInstitucional.Feriado)
            {
                dto.ContabilizaAsistencia = false;
            }
        }

        private static void ValidarCambioActividad(CrearEventoInstitucionalDto dto)
        {
            if (dto.CambioActividad && string.IsNullOrWhiteSpace(dto.ComentarioCambioActividad))
                throw new InvalidOperationException(
                    "Debe indicar un comentario cuando el evento implica cambio de actividad.");
        }

        private async Task ValidarSuperposicionPeriodoClasesAsync(
            DateOnly fechaInicio, DateOnly fechaFin, int anioLectivo, Guid? excluirId, CancellationToken ct)
        {
            var query = _context.EventosInstitucionales
                .Where(e => e.Activo
                    && e.TipoEvento == TipoEventoInstitucional.PeriodoDeClases
                    && e.AnioLectivo == anioLectivo
                    && e.FechaInicio <= fechaFin
                    && e.FechaFin >= fechaInicio);

            if (excluirId.HasValue)
                query = query.Where(e => e.IdEvento != excluirId.Value);

            if (await query.AnyAsync(ct))
                throw new InvalidOperationException(
                    "Ya existe un Período de Clases que se superpone con las fechas indicadas.");
        }

        private AuditoriaEventoInstitucional CrearAuditoria(
            Guid idEvento, TipoOperacionEvento tipo, string? antes, string? despues)
        {
            return new AuditoriaEventoInstitucional
            {
                IdAuditoria = Guid.NewGuid(),
                IdEvento = idEvento,
                TipoOperacion = tipo,
                ValoresAnteriores = antes,
                ValoresNuevos = despues,
                IdUsuario = _currentUser.UserId
                    ?? throw new UnauthorizedAccessException("No se pudo determinar el usuario actual."),
                FechaRegistro = DateTime.UtcNow,
            };
        }

        private static string SerializarEvento(EventoInstitucional e)
        {
            return JsonSerializer.Serialize(new
            {
                e.Titulo,
                e.Descripcion,
                TipoEvento = (int)e.TipoEvento,
                FechaInicio = e.FechaInicio.ToString("yyyy-MM-dd"),
                FechaFin = e.FechaFin.ToString("yyyy-MM-dd"),
                e.ContabilizaAsistencia,
                e.CambioActividad,
                e.ComentarioCambioActividad,
                CursoIds = e.Cursos.Select(c => c.IdCurso).ToList(),
            });
        }

        private static EventoInstitucionalDto MapToDto(EventoInstitucional e)
        {
            return new EventoInstitucionalDto
            {
                IdEvento = e.IdEvento,
                Titulo = e.Titulo,
                Descripcion = e.Descripcion,
                TipoEvento = (int)e.TipoEvento,
                TipoEventoLabel = TipoLabels.GetValueOrDefault(e.TipoEvento, e.TipoEvento.ToString()),
                FechaInicio = e.FechaInicio.ToString("yyyy-MM-dd"),
                FechaFin = e.FechaFin.ToString("yyyy-MM-dd"),
                ContabilizaAsistencia = e.ContabilizaAsistencia,
                CambioActividad = e.CambioActividad,
                ComentarioCambioActividad = e.ComentarioCambioActividad,
                AnioLectivo = e.AnioLectivo,
                Cursos = e.Cursos.Select(c => new CursoEventoDto
                {
                    IdCurso = c.IdCurso,
                    Label = $"{c.Curso.Anio.Numero}°{c.Curso.Division.Nombre}",
                }).ToList(),
            };
        }
    }
}
