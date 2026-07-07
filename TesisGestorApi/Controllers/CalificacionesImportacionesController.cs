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

        [HttpGet("ec/{idEC:guid}/activa")]
        public async Task<ActionResult<ImportacionCalificacionesDetalleDto>> GetActiva(Guid idEC, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var session = await _service.GetActivaPorECAsync(idEC, idDocente.Value, ct);
                return session == null ? NoContent() : Ok(session);
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
                _logger.LogError(ex, "Error al obtener la importación activa del EC {IdEC}.", idEC);
                return StatusCode(500, ErrorPayload("Error interno al obtener la importación activa."));
            }
        }

        [HttpPost("ec/{idEC:guid}/analizar")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ImportacionCalificacionesDetalleDto>> Analizar(
            Guid idEC,
            [FromForm] AnalizarImportacionCalificacionesDto dto,
            CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                var session = await _service.AnalizarAsync(idEC, idDocente.Value, dto, ct);
                return Ok(session);
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

        [HttpGet("{idImportacion:guid}")]
        public async Task<ActionResult<ImportacionCalificacionesDetalleDto>> GetDetalle(Guid idImportacion, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.GetDetalleAsync(idImportacion, idDocente.Value, ct));
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
                _logger.LogError(ex, "Error al obtener importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al obtener la importación."));
            }
        }

        [HttpPost("{idImportacion:guid}/reanalyze")]
        public async Task<ActionResult<ImportacionCalificacionesDetalleDto>> Reanalizar(Guid idImportacion, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.ReanalizarAsync(idImportacion, idDocente.Value, ct));
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
                _logger.LogError(ex, "Error al reanalizar importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al reanalizar la importación."));
            }
        }

        [HttpGet("{idImportacion:guid}/revision")]
        public async Task<ActionResult<ImportacionRevisionDto>> GetRevision(Guid idImportacion, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.GetRevisionAsync(idImportacion, idDocente.Value, ct));
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
                _logger.LogError(ex, "Error al obtener revisión de importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al obtener la revisión."));
            }
        }

        [HttpPut("{idImportacion:guid}/revision")]
        public async Task<ActionResult<ImportacionRevisionDto>> GuardarRevision(
            Guid idImportacion,
            [FromBody] ActualizarImportacionRevisionDto dto,
            CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.GuardarRevisionAsync(idImportacion, idDocente.Value, dto, ct));
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
                _logger.LogError(ex, "Error al guardar revisión de importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al guardar la revisión."));
            }
        }

        [HttpGet("{idImportacion:guid}/confirmacion")]
        public async Task<ActionResult<ImportacionConfirmacionDto>> GetConfirmacion(Guid idImportacion, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.GetConfirmacionAsync(idImportacion, idDocente.Value, ct));
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
                _logger.LogError(ex, "Error al obtener confirmación de importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al obtener la confirmación."));
            }
        }

        [HttpPost("{idImportacion:guid}/confirmar")]
        public async Task<ActionResult<ConfirmarImportacionCalificacionesResponseDto>> Confirmar(Guid idImportacion, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                return Ok(await _service.ConfirmarAsync(idImportacion, idDocente.Value, ct));
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
                _logger.LogError(ex, "Error al confirmar importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al confirmar la importación."));
            }
        }

        [HttpPost("{idImportacion:guid}/cancelar")]
        public async Task<IActionResult> Cancelar(Guid idImportacion, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            try
            {
                await _service.CancelarAsync(idImportacion, idDocente.Value, ct);
                return NoContent();
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
                _logger.LogError(ex, "Error al cancelar importación {IdImportacion}.", idImportacion);
                return StatusCode(500, ErrorPayload("Error interno al cancelar la importación."));
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
