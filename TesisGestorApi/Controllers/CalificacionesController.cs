using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TesisGestorApi.DTOs.Calificaciones;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/calificaciones")]
    public class CalificacionesController : ControllerBase
    {
        private readonly ICalificacionesService _service;
        private readonly ILogger<CalificacionesController> _logger;

        public CalificacionesController(ICalificacionesService service, ILogger<CalificacionesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("ec/{idEC:guid}/instancias")]
        public async Task<ActionResult<List<InstanciaEvaluativaResumenDto>>> GetInstancias(Guid idEC, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var items = await _service.GetInstanciasPorECAsync(idEC, idDocente.Value, ct);
                return Ok(items);
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
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener instancias evaluativas del EC {IdEC}.", idEC);
                return StatusCode(500, "Error interno al obtener las instancias evaluativas.");
            }
        }

        [HttpGet("ec/{idEC:guid}/estudiantes")]
        public async Task<ActionResult<List<GestionManualEstudianteDto>>> GetEstudiantes(Guid idEC, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var response = await _service.GetEstudiantesPorECAsync(idEC, idDocente.Value, ct);
                return Ok(response);
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
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los estudiantes de calificaciones para EC {IdEC}.", idEC);
                return StatusCode(500, "Error interno al obtener los estudiantes.");
            }
        }

        [HttpGet("ec/{idEC:guid}/calificaciones-vigentes")]
        public async Task<ActionResult<List<CalificacionVigenteDto>>> GetCalificacionesVigentes(Guid idEC, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var response = await _service.GetCalificacionesVigentesPorECAsync(idEC, idDocente.Value, ct);
                return Ok(response);
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
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener las calificaciones vigentes para EC {IdEC}.", idEC);
                return StatusCode(500, "Error interno al obtener las calificaciones vigentes.");
            }
        }

        [HttpPut("ec/{idEC:guid}/gestion-manual")]
        public async Task<ActionResult<GuardarCalificacionesManualResponseDto>> GuardarGestionManual(
            Guid idEC,
            [FromBody] GuardarCalificacionesManualDto dto,
            CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var response = await _service.GuardarGestionManualAsync(idEC, idDocente.Value, dto, ct);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar calificaciones manuales para EC {IdEC}.", idEC);
                return StatusCode(500, "Error interno al guardar las calificaciones.");
            }
        }

        [HttpGet("ec/{idEC:guid}/auditoria")]
        public async Task<ActionResult<AuditoriaCalificacionesResponseDto>> GetAuditoria(
            Guid idEC,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 5,
            CancellationToken ct = default)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var response = await _service.GetAuditoriaAsync(idEC, idDocente.Value, skip, take, ct);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener auditoría de calificaciones para EC {IdEC}.", idEC);
                return StatusCode(500, "Error interno al obtener la auditoría.");
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
