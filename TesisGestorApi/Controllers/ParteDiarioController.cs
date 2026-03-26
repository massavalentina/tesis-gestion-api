using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.ParteDiario;
using TesisGestorApi.Interfaces;

[ApiController]
[Route("api/parte-diario")]
public class ParteDiarioController : ControllerBase
{
    private readonly IParteDiarioService _service;
    private readonly IAsistenciaService  _asistenciaService;
    private readonly ILogger<ParteDiarioController> _logger;

    public ParteDiarioController(
        IParteDiarioService service,
        IAsistenciaService asistenciaService,
        ILogger<ParteDiarioController> logger)
    {
        _service           = service;
        _asistenciaService = asistenciaService;
        _logger            = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ParteDiarioResumenDto>> GetResumen(
        [FromQuery] Guid cursoId,
        [FromQuery] DateOnly fecha)
    {
        try
        {
            var resumen = await _service.ObtenerResumenAsync(cursoId, fecha);
            return Ok(resumen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener resumen del parte diario para curso {CursoId}, fecha {Fecha}.", cursoId, fecha);
            return StatusCode(500, "Error interno al obtener el parte diario.");
        }
    }

    [HttpGet("comentarios")]
    public async Task<ActionResult<List<ComentarioParteDto>>> GetComentarios(
        [FromQuery] Guid cursoId,
        [FromQuery] DateOnly fecha)
    {
        try
        {
            var comentarios = await _service.ObtenerComentariosAsync(cursoId, fecha);
            return Ok(comentarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener comentarios del parte para curso {CursoId}, fecha {Fecha}.", cursoId, fecha);
            return StatusCode(500, "Error interno al obtener los comentarios.");
        }
    }

    [HttpPost("comentarios")]
    public async Task<ActionResult<ComentarioParteDto>> AgregarComentario([FromBody] AgregarComentarioDto dto)
    {
        try
        {
            var resultado = await _service.AgregarComentarioAsync(dto);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar comentario al parte.");
            return StatusCode(500, "Error interno al agregar el comentario.");
        }
    }

    [HttpPost("horario/intercambiar")]
    public async Task<IActionResult> IntercambiarHorario([FromBody] IntercambiarHorarioDto dto)
    {
        try
        {
            await _service.IntercambiarHorarioClasesAsync(dto);
            await _asistenciaService.RecalcularAsistenciasCursoFechaAsync(dto.CursoId, dto.Fecha);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intercambiar horario de clases.");
            return StatusCode(500, "Error interno al intercambiar el horario.");
        }
    }

    [HttpPost("horario/resetear")]
    public async Task<IActionResult> ResetearHorario(
        [FromQuery] Guid idHorario,
        [FromQuery] Guid cursoId,
        [FromQuery] DateOnly fecha)
    {
        try
        {
            await _service.ResetearHorarioClaseAsync(idHorario, fecha, cursoId);
            await _asistenciaService.RecalcularAsistenciasCursoFechaAsync(cursoId, fecha);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al resetear horario de clase.");
            return StatusCode(500, "Error interno al restablecer el horario.");
        }
    }

    [HttpPost("horario/reorganizar")]
    public async Task<IActionResult> ReorganizarHorario([FromBody] ReorganizarHorarioDto dto)
    {
        try
        {
            await _service.ReorganizarHorarioAsync(dto);
            await _asistenciaService.RecalcularAsistenciasCursoFechaAsync(dto.CursoId, dto.Fecha);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reorganizar horario.");
            return StatusCode(500, "Error interno al reorganizar el horario.");
        }
    }
}
