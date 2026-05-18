using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Asignaciones;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/preceptores")]
    public class PreceptoresController : ControllerBase
    {
        private readonly IPreceptorService _service;
        private readonly ILogger<PreceptoresController> _logger;

        public PreceptoresController(IPreceptorService service, ILogger<PreceptoresController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // GET /api/preceptores/{idPreceptor}/cursos
        [HttpGet("{idPreceptor:guid}/cursos")]
        public async Task<IActionResult> GetCursos(Guid idPreceptor, CancellationToken ct)
        {
            try
            {
                var resultado = await _service.GetCursosAsync(idPreceptor, ct);
                return Ok(resultado);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cursos del preceptor {Id}.", idPreceptor);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/preceptores/{idPreceptor}/cursos
        [HttpPost("{idPreceptor:guid}/cursos")]
        public async Task<IActionResult> AsignarCurso(Guid idPreceptor, [FromBody] AsignarCursoDto dto, CancellationToken ct)
        {
            try
            {
                await _service.AsignarCursoAsync(idPreceptor, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar curso al preceptor {Id}.", idPreceptor);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/preceptores/{idPreceptor}/desasignar
        [HttpPost("{idPreceptor:guid}/desasignar")]
        public async Task<IActionResult> Desasignar(Guid idPreceptor, [FromBody] DesasignarPreceptorDto dto, CancellationToken ct)
        {
            try
            {
                await _service.DesasignarCursosAsync(idPreceptor, dto.Motivo, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desasignar cursos del preceptor {Id}.", idPreceptor);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/preceptores/{idPreceptor}/cursos/{idPreceptorCurso}/desasignar
        [HttpPost("{idPreceptor:guid}/cursos/{idPreceptorCurso:guid}/desasignar")]
        public async Task<IActionResult> DesasignarCurso(Guid idPreceptor, Guid idPreceptorCurso, [FromBody] DesasignarPreceptorDto dto, CancellationToken ct)
        {
            try
            {
                await _service.DesasignarCursoAsync(idPreceptor, idPreceptorCurso, dto.Motivo, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desasignar curso {CursoId} del preceptor {Id}.", idPreceptorCurso, idPreceptor);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // GET /api/cursos/sin-preceptor
        [HttpGet("/api/cursos/sin-preceptor")]
        public async Task<IActionResult> GetCursosSinPreceptor(CancellationToken ct)
        {
            try
            {
                var resultado = await _service.GetCursosSinPreceptorAsync(ct);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener cursos sin preceptor.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }
    }
}
