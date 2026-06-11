using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Asignaciones;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class DocenteService : IDocenteService
    {
        private readonly ApplicationDbContext _context;

        public DocenteService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AsignarEspacioCurricularAsync(Guid idDocente, AsignarECDto dto, CancellationToken ct = default)
        {
            var docente = await _context.Docentes
                .Include(d => d.Usuario)
                    .ThenInclude(u => u.UsuarioRoles)
                        .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(d => d.IdDocente == idDocente, ct)
                ?? throw new KeyNotFoundException($"No existe un docente con id '{idDocente}'.");

            if (!docente.Usuario.Activo)
                throw new InvalidOperationException("El docente no está activo.");

            var tieneRol = docente.Usuario.UsuarioRoles
                .Any(ur => ur.Rol.Nombre.Equals("Docente", StringComparison.OrdinalIgnoreCase));
            if (!tieneRol)
                throw new InvalidOperationException("El usuario no tiene el rol Docente activo.");

            var ec = await _context.EspaciosCurriculares
                .Include(e => e.Horarios)
                .FirstOrDefaultAsync(e => e.IdEC == dto.IdEC, ct)
                ?? throw new KeyNotFoundException($"No existe un espacio curricular con id '{dto.IdEC}'.");

            if (ec.IdDocente != null)
                throw new InvalidOperationException("El espacio curricular ya tiene un docente asignado.");

            // Validar conflicto de horario
            var horariosNuevo = ec.Horarios.ToList();
            if (horariosNuevo.Count > 0)
            {
                var ecsActivos = await _context.EspaciosCurriculares
                    .Include(e => e.Horarios)
                    .Where(e => e.IdDocente == idDocente)
                    .ToListAsync(ct);

                foreach (var ecExistente in ecsActivos)
                {
                    foreach (var hExistente in ecExistente.Horarios)
                    {
                        foreach (var hNuevo in horariosNuevo)
                        {
                            if (hExistente.DíaSemana == hNuevo.DíaSemana &&
                                hExistente.HorarioEntrada < hNuevo.HorarioSalida &&
                                hExistente.HorarioSalida > hNuevo.HorarioEntrada)
                            {
                                var diaEs = hExistente.DíaSemana.ToString() switch
                                {
                                    "Monday"    => "lunes",
                                    "Tuesday"   => "martes",
                                    "Wednesday" => "miércoles",
                                    "Thursday"  => "jueves",
                                    "Friday"    => "viernes",
                                    "Saturday"  => "sábado",
                                    "Sunday"    => "domingo",
                                    var d       => d,
                                };
                                throw new InvalidOperationException(
                                    $"Conflicto de horario: el docente ya tiene clases el {diaEs} " +
                                    $"de {hExistente.HorarioEntrada:hh\\:mm} a {hExistente.HorarioSalida:hh\\:mm}.");
                            }
                        }
                    }
                }
            }

            _context.DocentesEspaciosCurriculares.Add(new DocenteEspacioCurricular
            {
                IdDocenteEC = Guid.NewGuid(),
                IdDocente   = idDocente,
                IdEC        = dto.IdEC,
                FechaDesde  = DateTime.UtcNow,
                FechaHasta  = null,
                Motivo      = null,
            });

            ec.IdDocente = idDocente;

            await _context.SaveChangesAsync(ct);
        }

        public async Task DesasignarEspaciosCurricularesAsync(Guid idDocente, string motivo, CancellationToken ct = default)
        {
            var hoy = DateTime.UtcNow;

            var registrosActivos = await _context.DocentesEspaciosCurriculares
                .Where(d => d.IdDocente == idDocente && d.FechaHasta == null)
                .ToListAsync(ct);

            if (registrosActivos.Count == 0) return;

            foreach (var r in registrosActivos)
            {
                r.FechaHasta = hoy;
                r.Motivo     = motivo;
            }

            var idECs = registrosActivos.Select(r => r.IdEC).ToList();
            await _context.EspaciosCurriculares
                .Where(ec => idECs.Contains(ec.IdEC))
                .ExecuteUpdateAsync(s => s.SetProperty(ec => ec.IdDocente, (Guid?)null), ct);

            await _context.SaveChangesAsync(ct);
        }

        public async Task DesasignarEspacioCurricularAsync(Guid idDocente, Guid idDocenteEC, string motivo, CancellationToken ct = default)
        {
            var registro = await _context.DocentesEspaciosCurriculares
                .FirstOrDefaultAsync(d => d.IdDocenteEC == idDocenteEC && d.IdDocente == idDocente && d.FechaHasta == null, ct)
                ?? throw new KeyNotFoundException($"No se encontró una asignación activa con id '{idDocenteEC}'.");

            registro.FechaHasta = DateTime.UtcNow;
            registro.Motivo     = motivo;

            await _context.EspaciosCurriculares
                .Where(ec => ec.IdEC == registro.IdEC)
                .ExecuteUpdateAsync(s => s.SetProperty(ec => ec.IdDocente, (Guid?)null), ct);

            await _context.SaveChangesAsync(ct);
        }

        public async Task<DocenteECsResponseDto> GetEspaciosCurricularesAsync(Guid idDocente, CancellationToken ct = default)
        {
            var registros = await _context.DocentesEspaciosCurriculares
                .AsNoTracking()
                .Include(d => d.EspacioCurricular)
                    .ThenInclude(ec => ec.Curricula)
                .Include(d => d.EspacioCurricular)
                    .ThenInclude(ec => ec.Curso)
                        .ThenInclude(c => c.Anio)
                .Include(d => d.EspacioCurricular)
                    .ThenInclude(ec => ec.Curso)
                        .ThenInclude(c => c.Division)
                .Include(d => d.EspacioCurricular)
                    .ThenInclude(ec => ec.Horarios)
                .Where(d => d.IdDocente == idDocente)
                .ToListAsync(ct);

            var activos = registros
                .Where(r => r.FechaHasta == null)
                .OrderBy(r => r.EspacioCurricular.Curso.Anio?.Numero ?? 0)
                    .ThenBy(r => r.EspacioCurricular.Curso.Division?.Nombre ?? ' ')
                    .ThenBy(r => r.EspacioCurricular.Curricula.Nombre)
                .Select(r => new DocenteECActivoDto(
                    r.IdDocenteEC,
                    r.IdEC,
                    r.EspacioCurricular.Curricula.Nombre,
                    r.EspacioCurricular.Curricula.Codigo,
                    r.EspacioCurricular.Curso.Codigo,
                    r.FechaDesde,
                    r.EspacioCurricular.Horarios.Select(h => new HorarioDto(
                        h.DíaSemana.ToString(),
                        h.HorarioEntrada.ToString(@"hh\:mm"),
                        h.HorarioSalida.ToString(@"hh\:mm")
                    )).ToList()
                )).ToList();

            var historial = registros
                .Where(r => r.FechaHasta != null)
                .Select(r => new DocenteECHistorialDto(
                    r.IdDocenteEC,
                    r.IdEC,
                    r.EspacioCurricular.Curricula.Nombre,
                    r.EspacioCurricular.Curricula.Codigo,
                    r.EspacioCurricular.Curso.Codigo,
                    r.FechaDesde,
                    r.FechaHasta,
                    r.Motivo
                )).ToList();

            return new DocenteECsResponseDto(activos, historial);
        }

        public async Task<List<MisEcItemDto>> GetMisEspaciosCurricularesAsync(Guid idUsuario, CancellationToken ct = default)
        {
            var docente = await _context.Docentes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);

            if (docente == null) return [];

            var ecs = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Include(ec => ec.Curricula)
                .Include(ec => ec.Curso)
                    .ThenInclude(c => c.Anio)
                .Include(ec => ec.Curso)
                    .ThenInclude(c => c.Division)
                .Include(ec => ec.Curso)
                    .ThenInclude(c => c.DetallesCursado)
                .Include(ec => ec.Horarios)
                .Where(ec => ec.IdDocente == docente.IdDocente)
                .OrderBy(ec => ec.Curso.Anio.Numero)
                    .ThenBy(ec => ec.Curso.Division.Nombre)
                    .ThenBy(ec => ec.Curricula.Nombre)
                .ToListAsync(ct);

            return ecs.Select(ec => new MisEcItemDto(
                ec.IdEC,
                ec.IdCurso,
                ec.Curricula.Nombre,
                ec.Curso.Codigo,
                ec.Curso.Anio.Numero,
                ec.Curso.Division.Nombre.ToString(),
                ec.Curso.AñoLectivo.Year,
                ec.Curso.DetallesCursado.Count(d => d.Estado),
                ec.Horarios
                    .OrderBy(h => h.DíaSemana)
                    .Select(h => new HorarioDto(
                        h.DíaSemana.ToString(),
                        h.HorarioEntrada.ToString(@"hh\:mm"),
                        h.HorarioSalida.ToString(@"hh\:mm")
                    )).ToList()
            )).ToList();
        }

        public async Task<List<ECsinDocenteDto>> GetEspaciosCurricularesSinDocenteAsync(CancellationToken ct = default)
        {
            var ecs = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Include(ec => ec.Curricula)
                .Include(ec => ec.Curso)
                    .ThenInclude(c => c.Anio)
                .Include(ec => ec.Curso)
                    .ThenInclude(c => c.Division)
                .Include(ec => ec.Horarios)
                .Where(ec => ec.IdDocente == null)
                .OrderBy(ec => ec.Curso.Anio.Numero)
                    .ThenBy(ec => ec.Curso.Division.Nombre)
                    .ThenBy(ec => ec.Curricula.Nombre)
                .ToListAsync(ct);

            var ecIds = ecs.Select(ec => ec.IdEC).ToList();
            var conHistorial = (await _context.DocentesEspaciosCurriculares
                .Where(d => ecIds.Contains(d.IdEC))
                .Select(d => d.IdEC)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet();

            return ecs.Select(ec => new ECsinDocenteDto(
                ec.IdEC,
                ec.Curricula.Nombre,
                ec.Curricula.Codigo,
                ec.Curso.Codigo,
                conHistorial.Contains(ec.IdEC),
                ec.Horarios
                    .OrderBy(h => h.DíaSemana)
                    .Select(h => new HorarioDto(
                        h.DíaSemana.ToString(),
                        h.HorarioEntrada.ToString(@"hh\:mm"),
                        h.HorarioSalida.ToString(@"hh\:mm")
                    )).ToList()
            )).ToList();
        }
    }
}
