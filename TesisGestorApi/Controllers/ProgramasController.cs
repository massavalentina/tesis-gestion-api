using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TesisGestorApi.DTOs.Programas;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProgramasController : ControllerBase
    {
        private readonly IProgramaService _programaService;
        private readonly ILogger<ProgramasController> _logger;

        public ProgramasController(IProgramaService programaService, ILogger<ProgramasController> logger)
        {
            _programaService = programaService;
            _logger = logger;
        }

        [HttpGet("ec/{idEC:guid}")]
        public async Task<ActionResult<List<ProgramaResumenDto>>> GetProgramasPorEC(
            Guid idEC, CancellationToken ct)
        {
            try
            {
                var programas = await _programaService.GetProgramasPorECAsync(idEC, ct);
                return Ok(programas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener programas del EC {IdEC}.", idEC);
                return StatusCode(500, "Error interno al obtener los programas.");
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ProgramaDetalleDto>> GetPrograma(Guid id, CancellationToken ct)
        {
            try
            {
                var programa = await _programaService.GetProgramaAsync(id, ct);
                return Ok(programa);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener programa {Id}.", id);
                return StatusCode(500, "Error interno al obtener el programa.");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ProgramaDetalleDto>> Crear(
            [FromBody] CrearProgramaDto dto, CancellationToken ct)
        {
            try
            {
                var idDocente = GetIdDocente();
                if (idDocente == null)
                    return Forbid();

                var programa = await _programaService.CrearProgramaAsync(idDocente.Value, dto, ct);
                return CreatedAtAction(nameof(GetPrograma), new { id = programa.IdPrograma }, programa);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear programa.");
                return StatusCode(500, "Error interno al crear el programa.");
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ProgramaDetalleDto>> Actualizar(
            Guid id, [FromBody] CrearProgramaDto dto, CancellationToken ct)
        {
            try
            {
                var idDocente = GetIdDocente();
                if (idDocente == null)
                    return Forbid();

                var programa = await _programaService.ActualizarProgramaAsync(id, idDocente.Value, dto, ct);
                return Ok(programa);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar programa {Id}.", id);
                return StatusCode(500, "Error interno al actualizar el programa.");
            }
        }

        [HttpPatch("{id:guid}/estado")]
        public async Task<IActionResult> CambiarEstado(
            Guid id, [FromBody] CambiarEstadoProgramaDto dto, CancellationToken ct)
        {
            try
            {
                var idDocente = GetIdDocente();
                if (idDocente == null)
                    return Forbid();

                await _programaService.CambiarEstadoAsync(id, idDocente.Value, dto.Estado, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del programa {Id}.", id);
                return StatusCode(500, "Error interno al cambiar el estado.");
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
        {
            try
            {
                var idDocente = GetIdDocente();
                if (idDocente == null)
                    return Forbid();

                await _programaService.EliminarProgramaAsync(id, idDocente.Value, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar programa {Id}.", id);
                return StatusCode(500, "Error interno al eliminar el programa.");
            }
        }

        private Guid? GetIdDocente()
        {
            var idDocenteStr = User.FindFirstValue("idDocente");
            if (string.IsNullOrEmpty(idDocenteStr)) return null;
            return Guid.Parse(idDocenteStr);
        }
    }
}
