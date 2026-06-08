using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Asignaciones;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/docentes")]
    public class DocentesController : ControllerBase
    {
        private readonly IDocenteService _service;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<DocentesController> _logger;

        public DocentesController(IDocenteService service, ICurrentUserService currentUser, ILogger<DocentesController> logger)
        {
            _service     = service;
            _currentUser = currentUser;
            _logger      = logger;
        }

        // GET /api/docentes/mis-espacios-curriculares
        [HttpGet("mis-espacios-curriculares")]
        public async Task<IActionResult> GetMisEspaciosCurriculares(CancellationToken ct)
        {
            var idUsuario = _currentUser.UserId;
            if (idUsuario == null) return Unauthorized();

            try
            {
                var resultado = await _service.GetMisEspaciosCurricularesAsync(idUsuario.Value, ct);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener mis ECs del usuario {Id}.", idUsuario);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // GET /api/docentes/{idDocente}/espacios-curriculares
        [HttpGet("{idDocente:guid}/espacios-curriculares")]
        public async Task<IActionResult> GetEspaciosCurriculares(Guid idDocente, CancellationToken ct)
        {
            try
            {
                var resultado = await _service.GetEspaciosCurricularesAsync(idDocente, ct);
                return Ok(resultado);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ECs del docente {Id}.", idDocente);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/docentes/{idDocente}/espacios-curriculares
        [HttpPost("{idDocente:guid}/espacios-curriculares")]
        public async Task<IActionResult> AsignarEspacioCurricular(Guid idDocente, [FromBody] AsignarECDto dto, CancellationToken ct)
        {
            try
            {
                await _service.AsignarEspacioCurricularAsync(idDocente, dto, ct);
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
                _logger.LogError(ex, "Error al asignar EC al docente {Id}.", idDocente);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/docentes/{idDocente}/desasignar
        [HttpPost("{idDocente:guid}/desasignar")]
        public async Task<IActionResult> Desasignar(Guid idDocente, [FromBody] DesasignarDocenteDto dto, CancellationToken ct)
        {
            try
            {
                await _service.DesasignarEspaciosCurricularesAsync(idDocente, dto.Motivo, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desasignar ECs del docente {Id}.", idDocente);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/docentes/{idDocente}/espacios-curriculares/{idDocenteEC}/desasignar
        [HttpPost("{idDocente:guid}/espacios-curriculares/{idDocenteEC:guid}/desasignar")]
        public async Task<IActionResult> DesasignarEC(Guid idDocente, Guid idDocenteEC, [FromBody] DesasignarDocenteDto dto, CancellationToken ct)
        {
            try
            {
                await _service.DesasignarEspacioCurricularAsync(idDocente, idDocenteEC, dto.Motivo, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desasignar EC {ECId} del docente {Id}.", idDocenteEC, idDocente);
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // GET /api/espacios-curriculares/sin-docente
        [HttpGet("/api/espacios-curriculares/sin-docente")]
        public async Task<IActionResult> GetEspaciosCurricularesSinDocente(CancellationToken ct)
        {
            try
            {
                var resultado = await _service.GetEspaciosCurricularesSinDocenteAsync(ct);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ECs sin docente.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }
    }
}
