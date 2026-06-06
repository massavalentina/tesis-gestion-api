using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Retiro;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/retiro")]
    public class RetiroController : ControllerBase
    {
        private readonly IRetiroService _retiroService;
        private readonly ILogger<RetiroController> _logger;

        public RetiroController(IRetiroService retiroService, ILogger<RetiroController> logger)
        {
            _retiroService = retiroService;
            _logger        = logger;
        }

        /// <summary>Tutores del estudiante (para el selector del paso 2).</summary>
        [HttpGet("estudiante/{estudianteId:guid}/tutores")]
        public async Task<ActionResult<List<TutorEstudianteDto>>> GetTutores(Guid estudianteId)
        {
            try
            {
                var tutores = await _retiroService.ObtenerTutoresEstudianteAsync(estudianteId);
                return Ok(tutores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tutores del estudiante {EstudianteId}.", estudianteId);
                return StatusCode(500, "Error interno al obtener los tutores.");
            }
        }

        /// <summary>Retiros activos del día para un estudiante (vacío si no tiene).</summary>
        [HttpGet("activos")]
        public async Task<ActionResult<List<RetiroActivoDto>>> GetRetirosActivos(
            [FromQuery] Guid estudianteId,
            [FromQuery] DateOnly fecha)
        {
            try
            {
                var retiros = await _retiroService.ObtenerRetirosActivosAsync(estudianteId, fecha);
                return Ok(retiros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener retiros activos del estudiante {EstudianteId}.", estudianteId);
                return StatusCode(500, "Error interno al obtener los retiros activos.");
            }
        }

        /// <summary>Registrar un retiro anticipado.</summary>
        [HttpPost]
        public async Task<ActionResult<RetiroActivoDto>> RegistrarRetiro([FromBody] RegistrarRetiroDto dto)
        {
            try
            {
                var result = await _retiroService.RegistrarRetiroAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar retiro.");
                return StatusCode(500, "Error interno al registrar el retiro.");
            }
        }

        /// <summary>Actualizar hora de retiro y nombre de preceptor (corrección de datos).</summary>
        [HttpPut("{idRetiro:guid}")]
        public async Task<ActionResult<RetiroActivoDto>> ActualizarRetiro(Guid idRetiro, [FromBody] ActualizarRetiroDto dto)
        {
            try
            {
                var result = await _retiroService.ActualizarRetiroAsync(idRetiro, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar retiro {IdRetiro}.", idRetiro);
                return StatusCode(500, "Error interno al actualizar el retiro.");
            }
        }

        /// <summary>Cancelar un retiro anticipado (revierte asistencia y borra el retiro).</summary>
        [HttpDelete("{idRetiro:guid}")]
        public async Task<IActionResult> CancelarRetiro(Guid idRetiro, [FromBody] CancelarRetiroDto? dto)
        {
            try
            {
                await _retiroService.CancelarRetiroAsync(idRetiro, dto?.Motivo);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar retiro {IdRetiro}.", idRetiro);
                return StatusCode(500, "Error interno al cancelar el retiro.");
            }
        }

        /// <summary>Registrar el reingreso de un retiro con reingreso.</summary>
        [HttpPost("reingreso")]
        public async Task<ActionResult<RetiroActivoDto>> RegistrarReingreso([FromBody] RegistrarReingresoDto dto)
        {
            try
            {
                var result = await _retiroService.RegistrarReingresoAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar reingreso.");
                return StatusCode(500, "Error interno al registrar el reingreso.");
            }
        }
    }
}
