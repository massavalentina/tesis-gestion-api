using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;

namespace TesisGestorApi.Controllers
{
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
            var cursos = await _db.Cursos
                .Where(c => c.Estado)
                .Where(c => c.AñoLectivo.Year == anioLectivo)
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
            Guid id, CancellationToken ct = default)
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
                    documento = dc.Estudiante.Documento
                })
                .ToListAsync(ct);

            return Ok(estudiantes);
        }
    }
}