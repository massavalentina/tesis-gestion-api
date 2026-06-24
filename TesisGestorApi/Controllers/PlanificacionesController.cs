using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TesisGestorApi.DTOs.Planificaciones;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PlanificacionesController : ControllerBase
{
    private readonly IPlanificacionService _service;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<PlanificacionesController> _logger;

    public PlanificacionesController(
        IPlanificacionService service,
        ISupabaseStorageService storageService,
        ILogger<PlanificacionesController> logger)
    {
        _service        = service;
        _storageService = storageService;
        _logger         = logger;
    }

    // GET /api/planificaciones/ec/{idEC}
    [HttpGet("ec/{idEC:guid}")]
    public async Task<ActionResult<ArbolPlanificacionDto>> GetArbol(Guid idEC, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            var arbol = await _service.GetArbolAsync(idEC, idDocente.Value, ct);

            // Sin programa o bloqueado: devolver directo sin procesar URLs
            if (arbol.SinPrograma || arbol.Bloqueado)
                return Ok(arbol);

            if (!string.IsNullOrEmpty(arbol.UrlPrograma))
                arbol.UrlPrograma = _storageService.GetUrlPublica(arbol.UrlPrograma);

            foreach (var u in arbol.Unidades)
                foreach (var t in u.Temas)
                    foreach (var c in t.Clases)
                        if (!string.IsNullOrEmpty(c.Url))
                            c.Url = _storageService.GetUrlPublica(c.Url);

            return Ok(arbol);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener árbol de planificación para EC {IdEC}.", idEC);
            return StatusCode(500, "Error interno al obtener la planificación.");
        }
    }

    // POST /api/planificaciones/ec/{idEC}/unidades
    [HttpPost("ec/{idEC:guid}/unidades")]
    public async Task<ActionResult<UnidadArbolDto>> CrearUnidad(
        Guid idEC, [FromBody] CrearItemArchivoDto dto, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            var result = await _service.CrearUnidadAsync(idEC, idDocente.Value, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear unidad para EC {IdEC}.", idEC);
            return StatusCode(500, "Error interno al crear la unidad.");
        }
    }

    // POST /api/planificaciones/ec/{idEC}/unidades/{idUnidad}/temas
    [HttpPost("ec/{idEC:guid}/unidades/{idUnidad:guid}/temas")]
    public async Task<ActionResult<TemaArbolDto>> CrearTema(
        Guid idEC, Guid idUnidad, [FromBody] CrearItemArchivoDto dto, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            var result = await _service.CrearTemaAsync(idEC, idUnidad, idDocente.Value, dto, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear tema en unidad {IdUnidad}.", idUnidad);
            return StatusCode(500, "Error interno al crear el tema.");
        }
    }

    // POST /api/planificaciones/ec/{idEC}/clases
    [HttpPost("ec/{idEC:guid}/clases")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ClasePlanificacionDto>> CrearClase(
        Guid idEC, [FromForm] CrearClaseDto dto, CancellationToken ct)
    {
        var idDocente = GetIdDocente();
        if (idDocente is null) return Forbid();

        string? urlArchivo = null;
        if (dto.Archivo is not null)
        {
            if (!dto.Archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("El archivo adjunto debe ser un PDF.");
            if (dto.Archivo.Length > 50 * 1024 * 1024)
                return BadRequest("El archivo no puede superar los 50 MB.");

            var ruta = $"planificaciones/{Guid.NewGuid()}.pdf";
            await using var stream = dto.Archivo.OpenReadStream();
            await _storageService.SubirArchivoAsync(stream, ruta, "application/pdf", ct);
            urlArchivo = ruta;
        }

        try
        {
            var result = await _service.CrearClaseAsync(idEC, idDocente.Value, dto, urlArchivo, ct);
            if (!string.IsNullOrEmpty(result.Url))
                result.Url = _storageService.GetUrlPublica(result.Url);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear clase para EC {IdEC}.", idEC);
            return StatusCode(500, "Error interno al crear la clase.");
        }
    }

    // PUT /api/planificaciones/clases/{idClase}
    [HttpPut("clases/{idClase:guid}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ClasePlanificacionDto>> EditarClase(
        Guid idClase, [FromForm] EditarClaseDto dto, CancellationToken ct)
    {
        var idDocente = GetIdDocente();
        if (idDocente is null) return Forbid();

        string? urlArchivo = null;
        if (dto.Archivo is not null)
        {
            if (!dto.Archivo.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("El archivo adjunto debe ser un PDF.");

            var ruta = $"planificaciones/{Guid.NewGuid()}.pdf";
            await using var stream = dto.Archivo.OpenReadStream();
            await _storageService.SubirArchivoAsync(stream, ruta, "application/pdf", ct);
            urlArchivo = ruta;
        }

        try
        {
            var result = await _service.EditarClaseAsync(idClase, idDocente.Value, dto, urlArchivo, ct);
            if (!string.IsNullOrEmpty(result.Url))
                result.Url = _storageService.GetUrlPublica(result.Url);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar clase {IdClase}.", idClase);
            return StatusCode(500, "Error interno al editar la clase.");
        }
    }

    // PATCH /api/planificaciones/clases/{idClase}/estado
    [HttpPatch("clases/{idClase:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(
        Guid idClase, [FromBody] CambiarEstadoClaseDto dto, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            await _service.CambiarEstadoClaseAsync(idClase, idDocente.Value, dto.Estado, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estado de clase {IdClase}.", idClase);
            return StatusCode(500, "Error interno al cambiar el estado.");
        }
    }

    // DELETE /api/planificaciones/clases/{idClase}
    [HttpDelete("clases/{idClase:guid}")]
    public async Task<IActionResult> EliminarClase(Guid idClase, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            await _service.EliminarClaseAsync(idClase, idDocente.Value, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar clase {IdClase}.", idClase);
            return StatusCode(500, "Error interno al eliminar la clase.");
        }
    }

    // PATCH /api/planificaciones/bloques/{idBloque}/estado
    [HttpPatch("bloques/{idBloque:guid}/estado")]
    public async Task<IActionResult> CambiarEstadoBloque(
        Guid idBloque, [FromBody] CambiarEstadoClaseDto dto, CancellationToken ct)
    {
        try
        {
            var idDocente = GetIdDocente();
            if (idDocente is null) return Forbid();

            await _service.CambiarEstadoBloqueAsync(idBloque, idDocente.Value, dto.Estado, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estado del bloque {IdBloque}.", idBloque);
            return StatusCode(500, "Error interno al cambiar el estado del bloque.");
        }
    }

    private Guid? GetIdDocente()
    {
        var idDocenteStr = User.FindFirstValue("idDocente");
        if (string.IsNullOrEmpty(idDocenteStr)) return null;
        return Guid.Parse(idDocenteStr);
    }
}
