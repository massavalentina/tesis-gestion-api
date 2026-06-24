using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TesisGestorApi.DTOs.Evaluaciones;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EvaluacionesController : ControllerBase
{
    private readonly IEvaluacionesService _service;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<EvaluacionesController> _logger;

    public EvaluacionesController(
        IEvaluacionesService service,
        ISupabaseStorageService storageService,
        ILogger<EvaluacionesController> logger)
    {
        _service = service;
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet("ec/{idEC:guid}")]
    public async Task<ActionResult<GestionEvaluacionesDto>> GetGestion(Guid idEC, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            var gestion = await _service.GetGestionAsync(idEC, idDocente.Value, ct);
            MapUrlsPublicas(gestion);
            return Ok(gestion);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la gestión de evaluaciones para el EC {IdEC}.", idEC);
            return StatusCode(500, "Error interno al obtener la gestión de evaluaciones.");
        }
    }

    [HttpPut("ec/{idEC:guid}/instancias/{nro:int}/archivos/{tipo}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<InstanciaEvaluativaSlotDto>> GuardarArchivo(
        Guid idEC,
        int nro,
        string tipo,
        [FromForm] GuardarArchivoIEFormDto dto,
        CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            string? rutaArchivo = null;
            if (dto.Archivo is not null)
            {
                if (!dto.Archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("El archivo adjunto debe ser un PDF.");
                if (dto.Archivo.Length > 50 * 1024 * 1024)
                    return BadRequest("El archivo no puede superar los 50 MB.");

                rutaArchivo = $"evaluaciones/{idEC}/{nro}/{tipo}/{Guid.NewGuid():N}.pdf";
                await using var stream = dto.Archivo.OpenReadStream();
                await _storageService.SubirArchivoAsync(stream, rutaArchivo, "application/pdf", ct);
            }

            var slot = await _service.GuardarArchivoAsync(idEC, idDocente.Value, nro, tipo, dto, rutaArchivo, ct);
            MapUrlsPublicas(slot);
            return Ok(slot);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el archivo de la IE {Nro} ({Tipo}) del EC {IdEC}.", nro, tipo, idEC);
            return StatusCode(500, new { message = "Error interno al guardar el archivo." });
        }
    }

    [HttpPut("ec/{idEC:guid}/instancias/{nro:int}/estado")]
    public async Task<ActionResult<InstanciaEvaluativaSlotDto>> CambiarEstado(
        Guid idEC,
        int nro,
        [FromBody] CambiarEstadoIEFormDto dto,
        CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            var slot = await _service.CambiarEstadoAsync(idEC, idDocente.Value, nro, dto, ct);
            MapUrlsPublicas(slot);
            return Ok(slot);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar el estado de la IE {Nro} del EC {IdEC}.", nro, idEC);
            return StatusCode(500, new { message = "Error interno al actualizar el estado de la IE." });
        }
    }

    [HttpDelete("ec/{idEC:guid}/instancias/{nro:int}/archivos/{tipo}")]
    public async Task<IActionResult> EliminarArchivo(Guid idEC, int nro, string tipo, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            await _service.EliminarArchivoAsync(idEC, idDocente.Value, nro, tipo, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar el archivo de la IE {Nro} ({Tipo}) del EC {IdEC}.", nro, tipo, idEC);
            return StatusCode(500, new { message = "Error interno al eliminar el archivo." });
        }
    }

    private Guid? GetIdDocente()
    {
        var idDocenteStr = User.FindFirst("idDocente")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(idDocenteStr, out var idDocente))
            return idDocente;

        return null;
    }

    private void MapUrlsPublicas(GestionEvaluacionesDto dto)
    {
        foreach (var slot in dto.Instancias)
        {
            MapUrl(slot.NotaOriginal);
            MapUrl(slot.Recuperatorio1);
            MapUrl(slot.Recuperatorio2);
        }

        void MapUrl(ArchivoIETrazadoDto? archivo)
        {
            if (archivo is null || string.IsNullOrWhiteSpace(archivo.UrlArchivo))
            {
                return;
            }

            archivo.UrlArchivo = _storageService.GetUrlPublica(archivo.UrlArchivo);
        }
    }

    private void MapUrlsPublicas(InstanciaEvaluativaSlotDto dto)
    {
        MapUrl(dto.NotaOriginal);
        MapUrl(dto.Recuperatorio1);
        MapUrl(dto.Recuperatorio2);

        void MapUrl(ArchivoIETrazadoDto? archivo)
        {
            if (archivo is null || string.IsNullOrWhiteSpace(archivo.UrlArchivo))
            {
                return;
            }

            archivo.UrlArchivo = _storageService.GetUrlPublica(archivo.UrlArchivo);
        }
    }
}
