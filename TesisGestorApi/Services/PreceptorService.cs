using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Asignaciones;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class PreceptorService : IPreceptorService
    {
        private readonly ApplicationDbContext _context;

        public PreceptorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AsignarCursoAsync(Guid idPreceptor, AsignarCursoDto dto, CancellationToken ct = default)
        {
            var preceptor = await _context.Preceptores
                .Include(p => p.Usuario)
                    .ThenInclude(u => u.UsuarioRoles)
                        .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(p => p.IdPreceptor == idPreceptor, ct)
                ?? throw new KeyNotFoundException($"No existe un preceptor con id '{idPreceptor}'.");

            if (!preceptor.Usuario.Activo)
                throw new InvalidOperationException("El preceptor no está activo.");

            var tieneRol = preceptor.Usuario.UsuarioRoles
                .Any(ur => ur.Rol.Nombre.Equals("Preceptor", StringComparison.OrdinalIgnoreCase));
            if (!tieneRol)
                throw new InvalidOperationException("El usuario no tiene el rol Preceptor activo.");

            var curso = await _context.Cursos
                .FirstOrDefaultAsync(c => c.IdCurso == dto.IdCurso, ct)
                ?? throw new KeyNotFoundException($"No existe un curso con id '{dto.IdCurso}'.");

            if (curso.IdPreceptor != null)
                throw new InvalidOperationException("El curso ya tiene un preceptor asignado.");

            _context.PreceptoresCursos.Add(new PreceptorCurso
            {
                IdPreceptorCurso = Guid.NewGuid(),
                IdPreceptor      = idPreceptor,
                IdCurso          = dto.IdCurso,
                FechaDesde       = DateTime.UtcNow,
                FechaHasta       = null,
                Motivo           = null,
            });

            curso.IdPreceptor = idPreceptor;

            await _context.SaveChangesAsync(ct);
        }

        public async Task DesasignarCursosAsync(Guid idPreceptor, string motivo, CancellationToken ct = default)
        {
            var hoy = DateTime.UtcNow;

            var registrosActivos = await _context.PreceptoresCursos
                .Where(p => p.IdPreceptor == idPreceptor && p.FechaHasta == null)
                .ToListAsync(ct);

            if (registrosActivos.Count == 0) return;

            foreach (var r in registrosActivos)
            {
                r.FechaHasta = hoy;
                r.Motivo     = motivo;
            }

            var idCursos = registrosActivos.Select(r => r.IdCurso).ToList();
            await _context.Cursos
                .Where(c => idCursos.Contains(c.IdCurso))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IdPreceptor, (Guid?)null), ct);

            await _context.SaveChangesAsync(ct);
        }

        public async Task DesasignarCursoAsync(Guid idPreceptor, Guid idPreceptorCurso, string motivo, CancellationToken ct = default)
        {
            var registro = await _context.PreceptoresCursos
                .FirstOrDefaultAsync(p => p.IdPreceptorCurso == idPreceptorCurso && p.IdPreceptor == idPreceptor && p.FechaHasta == null, ct)
                ?? throw new KeyNotFoundException($"No se encontró una asignación activa con id '{idPreceptorCurso}'.");

            registro.FechaHasta = DateTime.UtcNow;
            registro.Motivo     = motivo;

            await _context.Cursos
                .Where(c => c.IdCurso == registro.IdCurso)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IdPreceptor, (Guid?)null), ct);

            await _context.SaveChangesAsync(ct);
        }

        public async Task<PreceptorCursosResponseDto> GetCursosAsync(Guid idPreceptor, CancellationToken ct = default)
        {
            var registros = await _context.PreceptoresCursos
                .AsNoTracking()
                .Include(p => p.Curso)
                    .ThenInclude(c => c.Anio)
                .Include(p => p.Curso)
                    .ThenInclude(c => c.Division)
                .Where(p => p.IdPreceptor == idPreceptor)
                .OrderByDescending(p => p.FechaDesde)
                .ToListAsync(ct);

            var activos = registros
                .Where(r => r.FechaHasta == null)
                .Select(r => new PreceptorCursoActivoDto(
                    r.IdPreceptorCurso,
                    r.IdCurso,
                    r.Curso.Codigo,
                    r.Curso.Anio.Numero,
                    r.Curso.Division.Nombre,
                    r.FechaDesde
                )).ToList();

            var historial = registros
                .Where(r => r.FechaHasta != null)
                .Select(r => new PreceptorCursoHistorialDto(
                    r.IdPreceptorCurso,
                    r.IdCurso,
                    r.Curso.Codigo,
                    r.Curso.Anio.Numero,
                    r.Curso.Division.Nombre,
                    r.FechaDesde,
                    r.FechaHasta,
                    r.Motivo
                )).ToList();

            return new PreceptorCursosResponseDto(activos, historial);
        }

        public async Task<List<CursoSinPreceptorDto>> GetCursosSinPreceptorAsync(CancellationToken ct = default)
        {
            var cursos = await _context.Cursos
                .AsNoTracking()
                .Include(c => c.Anio)
                .Include(c => c.Division)
                .Where(c => c.IdPreceptor == null)
                .ToListAsync(ct);

            var cursoIds = cursos.Select(c => c.IdCurso).ToList();
            var conHistorial = (await _context.PreceptoresCursos
                .Where(p => cursoIds.Contains(p.IdCurso))
                .Select(p => p.IdCurso)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet();

            return cursos.Select(c => new CursoSinPreceptorDto(
                c.IdCurso,
                c.Codigo,
                c.Anio.Numero,
                c.Division.Nombre,
                conHistorial.Contains(c.IdCurso)
            )).ToList();
        }
    }
}
