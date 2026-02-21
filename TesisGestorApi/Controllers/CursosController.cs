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
        public async Task<IActionResult> GetCursos([FromQuery] int anioLectivo = 2026, CancellationToken ct = default)
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
    }
}