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
        private readonly ISupabaseStorageService _storageService;
        private readonly ILogger<ProgramasController> _logger;

        public ProgramasController(
            IProgramaService programaService,
            ISupabaseStorageService storageService,
            ILogger<ProgramasController> logger)
        {
            _programaService = programaService;
            _storageService  = storageService;
            _logger          = logger;
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
                if (!string.IsNullOrEmpty(programa.Url))
                    programa.Url = _storageService.GetUrlPublica(programa.Url);
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

        [HttpPost("archivo")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ProgramaDetalleDto>> CargarDesdeArchivo(
            [FromForm] CargarProgramaArchivoDto dto, CancellationToken ct)
        {
            var idDocente = GetIdDocente();
            if (idDocente == null) return Forbid();

            if (!dto.Archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("El archivo debe ser un PDF.");
            if (dto.Archivo.Length > 50 * 1024 * 1024)
                return BadRequest("El archivo no puede superar los 50 MB.");

            try
            {
                var ruta = $"programas/subidos/{Guid.NewGuid()}.pdf";
                await using var stream = dto.Archivo.OpenReadStream();
                await _storageService.SubirArchivoAsync(stream, ruta, "application/pdf", ct);

                var programa = await _programaService.CrearDesdeArchivoAsync(idDocente.Value, dto, ruta, ct);
                if (!string.IsNullOrEmpty(programa.Url))
                    programa.Url = _storageService.GetUrlPublica(programa.Url);
                return CreatedAtAction(nameof(GetPrograma), new { id = programa.IdPrograma }, programa);
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
                _logger.LogError(ex, "Error al cargar programa desde archivo.");
                return StatusCode(500, "Error interno al cargar el programa.");
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
