
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/cursos")]
    public class CursosController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public CursosController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/cursos?anioLectivo=2026
        [HttpGet]
        public async Task<IActionResult> GetCursos(
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            var idDocente = await GetIdDocenteAsync(ct);

            var query = _db.Cursos
                .Where(c => c.Estado)
                .Where(c => c.AñoLectivo.Year == anioLectivo);

            if (idDocente.HasValue)
                query = query.Where(c => c.EspaciosCurriculares.Any(ec => ec.IdDocente == idDocente));

            var cursos = await query
                .OrderBy(c => c.Codigo)
                .Select(c => new
                {
                    idCurso = c.IdCurso,
                    codigo = c.Codigo
                })
                .ToListAsync(ct);

            return Ok(cursos);
        }

        // GET /api/cursos/{id}/estudiantes
        [HttpGet("{id:guid}/estudiantes")]
        public async Task<IActionResult> GetEstudiantes(
            Guid id,
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            var existe = await _db.Cursos.AnyAsync(c => c.IdCurso == id, ct);
            if (!existe) return NotFound($"No se encontró el curso con ID {id}.");

            var estudiantes = await _db.DetallesCursado
                .Where(dc => dc.IdCurso == id && dc.Estado)
                .OrderBy(dc => dc.Estudiante.Apellido)
                .ThenBy(dc => dc.Estudiante.Nombre)
                .Select(dc => new
                {
                    idEstudiante = dc.Estudiante.IdEstudiante,
                    nombre = dc.Estudiante.Nombre,
                    apellido = dc.Estudiante.Apellido,
                    documento = dc.Estudiante.Documento,
                    faltasAcumuladas = _db.AsistenciasResumenAnual
                        .Where(r => r.IdEstudiante == dc.Estudiante.IdEstudiante && r.AnioLectivo == anioLectivo)
                        .Select(r => r.FaltasAcumuladas)
                        .FirstOrDefault(),
                    teaGeneral = _db.AsistenciasResumenAnual
                        .Where(r => r.IdEstudiante == dc.Estudiante.IdEstudiante && r.AnioLectivo == anioLectivo)
                        .Select(r => r.TeaGeneral)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            return Ok(estudiantes);
        }

        // GET /api/cursos/{cursoId}/espacios-curriculares
        [HttpGet("{cursoId:guid}/espacios-curriculares")]
        public async Task<IActionResult> GetEspaciosCurriculares(
            Guid cursoId,
            CancellationToken ct = default)
        {
            var idDocente = await GetIdDocenteAsync(ct);

            var query = _db.EspaciosCurriculares
                .AsNoTracking()
                .Where(ec => ec.IdCurso == cursoId);

            if (idDocente.HasValue)
                query = query.Where(ec => ec.IdDocente == idDocente);

            var espacios = await query
                .Include(ec => ec.Curricula)
                .OrderBy(ec => ec.Curricula.Nombre)
                .Select(ec => new EspacioCurricularDto
                {
                    IdEC = ec.IdEC,
                    Nombre = ec.Curricula.Nombre
                })
                .ToListAsync(ct);

            return Ok(espacios);
        }

        // GET /api/cursos/buscar-estudiantes?texto=xxx&anioLectivo=2026
        [HttpGet("buscar-estudiantes")]
        public async Task<IActionResult> BuscarEstudiantes(
            [FromQuery] string texto,
            [FromQuery] int anioLectivo = 2026,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
                return BadRequest("Se requieren al menos 3 caracteres.");

            var t = texto.Trim().ToLower();
            var idDocente = await GetIdDocenteAsync(ct);

            var query = _db.DetallesCursado
                .Where(dc => dc.Estado
                    && dc.Curso.Estado
                    && dc.Curso.AñoLectivo.Year == anioLectivo
                    && (dc.Estudiante.Apellido.ToLower().StartsWith(t)
                        || dc.Estudiante.Documento.StartsWith(t)));

            if (idDocente.HasValue)
                query = query.Where(dc => dc.Curso.EspaciosCurriculares.Any(ec => ec.IdDocente == idDocente));

            var resultados = await query
                .OrderBy(dc => dc.Estudiante.Apellido)
                .ThenBy(dc => dc.Estudiante.Nombre)
                .Select(dc => new
                {
                    idEstudiante = dc.Estudiante.IdEstudiante,
                    nombre = dc.Estudiante.Nombre,
                    apellido = dc.Estudiante.Apellido,
                    documento = dc.Estudiante.Documento,
                    codigoCurso = dc.Curso.Codigo,
                    idCurso = dc.Curso.IdCurso,
                    teaGeneral = _db.AsistenciasResumenAnual
                        .Where(r => r.IdEstudiante == dc.Estudiante.IdEstudiante && r.AnioLectivo == anioLectivo)
                        .Select(r => r.TeaGeneral)
                        .FirstOrDefault()
                })
                .Take(20)
                .ToListAsync(ct);

            return Ok(resultados);
        }

        private async Task<Guid?> GetIdDocenteAsync(CancellationToken ct)
        {
            var esDocente = User.FindAll("roles").Any(c => c.Value == "Docente");
            if (!esDocente) return null;
            var idUsuarioStr = User.FindFirstValue("idUsuario");
            if (idUsuarioStr == null) return Guid.Empty;
            var idUsuario = Guid.Parse(idUsuarioStr);
            var docente = await _db.Docentes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);
            return docente?.IdDocente ?? Guid.Empty;
        }
    }
}