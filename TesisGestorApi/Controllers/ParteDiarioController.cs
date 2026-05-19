using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.ParteDiario;
using TesisGestorApi.Interfaces;

[Authorize]
[ApiController]
[Route("api/parte-diario")]
public class ParteDiarioController : ControllerBase
{
    private readonly IParteDiarioService _service;
    private readonly IAsistenciaService  _asistenciaService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ParteDiarioController> _logger;

    public ParteDiarioController(
        IParteDiarioService service,
        IAsistenciaService asistenciaService,
        ApplicationDbContext context,
        ILogger<ParteDiarioController> logger)
    {
        _service           = service;
        _asistenciaService = asistenciaService;
        _context           = context;
        _logger            = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ParteDiarioResumenDto>> GetResumen(
        [FromQuery] Guid cursoId,
        [FromQuery] DateOnly fecha,
        CancellationToken ct = default)
    {
        if (!await TieneAccesoCurso(cursoId, ct)) return Forbid();
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
        [FromQuery] DateOnly fecha,
        CancellationToken ct = default)
    {
        if (!await TieneAccesoCurso(cursoId, ct)) return Forbid();
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
    public async Task<ActionResult<ComentarioParteDto>> AgregarComentario(
        [FromBody] AgregarComentarioDto dto,
        CancellationToken ct = default)
    {
        if (!await TieneAccesoCurso(dto.CursoId, ct)) return Forbid();
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
    public async Task<IActionResult> IntercambiarHorario(
        [FromBody] IntercambiarHorarioDto dto,
        CancellationToken ct = default)
    {
        if (!await TieneAccesoCurso(dto.CursoId, ct)) return Forbid();
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
        [FromQuery] DateOnly fecha,
        CancellationToken ct = default)
    {
        if (!await TieneAccesoCurso(cursoId, ct)) return Forbid();
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
    public async Task<IActionResult> ReorganizarHorario(
        [FromBody] ReorganizarHorarioDto dto,
        CancellationToken ct = default)
    {
        if (!await TieneAccesoCurso(dto.CursoId, ct)) return Forbid();
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

    private async Task<bool> TieneAccesoCurso(Guid cursoId, CancellationToken ct)
    {
        var roles = User.FindAll("roles").Select(c => c.Value).ToList();
        var esDocente = roles.Contains("Docente");
        _logger.LogInformation("[PARTE] Roles: [{Roles}], esDocente={EsDocente}, cursoId={CursoId}", string.Join(",", roles), esDocente, cursoId);
        if (!esDocente) return true;

        var idUsuarioStr = User.FindFirstValue("idUsuario");
        _logger.LogInformation("[PARTE] idUsuario del token: {IdUsuario}", idUsuarioStr);
        if (idUsuarioStr == null) return false;
        var idUsuario = Guid.Parse(idUsuarioStr);

        var docente = await _context.Docentes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);
        _logger.LogInformation("[PARTE] Docente encontrado: {Resultado}, IdDocente: {IdDocente}", docente != null, docente?.IdDocente);
        if (docente == null) return false;

        var tieneEC = await _context.EspaciosCurriculares
            .AnyAsync(ec => ec.IdDocente == docente.IdDocente && ec.IdCurso == cursoId, ct);
        _logger.LogInformation("[PARTE] TieneEC en curso {CursoId}: {TieneEC}", cursoId, tieneEC);
        return tieneEC;
    }
}
