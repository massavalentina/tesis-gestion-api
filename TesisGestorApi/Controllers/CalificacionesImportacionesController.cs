using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TesisGestorApi.DTOs.CalificacionesImportacion;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/calificaciones/importaciones")]
    public class CalificacionesImportacionesController : ControllerBase
    {
        private readonly ICalificacionesImportacionService _service;
        private readonly ILogger<CalificacionesImportacionesController> _logger;

        private static object ErrorPayload(string message) => new { message };

        public CalificacionesImportacionesController(
            ICalificacionesImportacionService service,
            ILogger<CalificacionesImportacionesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("ec/{idEC:guid}/analizar")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ImportacionCalificacionesAnalisisDto>> Analizar(
            Guid idEC,
            [FromForm] AnalizarImportacionCalificacionesDto dto,
            CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.AnalizarAsync(idEC, idDocente.Value, dto, ct));
            }
            catch (ValidationException ex)
            {
                return BadRequest(ErrorPayload(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ErrorPayload(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ErrorPayload(ex.Message));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al analizar importación de calificaciones para EC {IdEC}.", idEC);
                return StatusCode(500, ErrorPayload("Error interno al analizar la importación."));
            }
        }

        [HttpPost("ec/{idEC:guid}/confirmar")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ConfirmarImportacionCalificacionesResponseDto>> Confirmar(
            Guid idEC,
            [FromForm] ConfirmarImportacionCalificacionesDto dto,
            CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.ConfirmarAsync(idEC, idDocente.Value, dto, ct));
            }
            catch (ValidationException ex)
            {
                return BadRequest(ErrorPayload(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ErrorPayload(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ErrorPayload(ex.Message));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al confirmar importación para EC {IdEC}.", idEC);
                return StatusCode(500, ErrorPayload("Error interno al confirmar la importación."));
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
