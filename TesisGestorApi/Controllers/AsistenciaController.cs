using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Claims;
using TesisGestorApi.Data;
using TesisGestorApi.Dtos;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;
using TesisGestorApi.DTOs;
using TesisGestorApi.Services;


[Authorize]
    [ApiController]
[Route("api/[controller]")]

public class AsistenciaController : ControllerBase
{
    private IAsistenciaService _asistenciaService; // Servicio para lógica de negocio (cálculo de asistencias)
    private readonly ApplicationDbContext _context; // Conexión a Db
    private readonly ILogger<AsistenciaController> _logger; // Logger de errores
    private readonly IAuditoriaAsistenciaECService _auditoriaECService;

    public AsistenciaController(ApplicationDbContext context, ILogger<AsistenciaController> logger, IAsistenciaService asistenciaService, IAuditoriaAsistenciaECService auditoriaECService)
    {
        _context = context; // Inyección de Dependencias
        _logger = logger;
        _asistenciaService = asistenciaService;
        _auditoriaECService = auditoriaECService;
    }

    [HttpGet("cursos/{cursoId:guid}/turnos")]
    public async Task<ActionResult> GetTurnosCurso(Guid cursoId, [FromQuery] DateOnly fecha)
    {
        try
        {
            var limite   = new TimeSpan(13, 20, 0);
            var diaSemana = fecha.DayOfWeek;

            var tieneTarde = await _context.EspaciosCurriculares
                .AsNoTracking()
                .Where(ec => ec.IdCurso == cursoId)
                .AnyAsync(ec => ec.Horarios.Any(h => h.DíaSemana == diaSemana && h.HorarioEntrada >= limite));

            return Ok(new { tieneTarde });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar turnos del curso {CursoId}.", cursoId);
            return StatusCode(500, "Error interno al verificar los turnos.");
        }
    }

    [HttpGet("cursos")]
    public async Task<ActionResult<List<OpcionSeleccionDto>>> GetCursos(CancellationToken ct = default)
    {
        try
        {
            var cursos = await _asistenciaService.ObtenerCursosAsync(await GetIdDocenteAsync(ct));
            return Ok(cursos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener cursos.");
            return StatusCode(500, "Error interno al obtener los cursos.");
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AsistenciaGetDTO>>> GetAsistencias(
     [FromQuery] DateOnly? fecha,
     [FromQuery] Guid? estudianteId)
    {
        try
        {
            // Pasamos el DateOnly directamente al servicio
            var resultados = await _asistenciaService.ObtenerAsistenciasAsync(fecha, estudianteId);
            return Ok(resultados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener asistencias");
            return StatusCode(500, "Error interno al obtener los datos.");
        }
    }

    [HttpPost] // Petición POST para Asistencia Individual
    public async Task<ActionResult<AsistenciaResponseDto>> RegistrarIndividual(
        [FromBody] RegistrarAsistenciaDto request)
    {
        try
        {
            var resultado = await _asistenciaService.RegistrarAsistenciaIndividualAsync(request);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar asistencia individual para estudiante {EstudianteId}", request.EstudianteId);
            return BadRequest(ex.Message);
        }
    }


    [HttpPost("lote")] // Petición POST para Lote de Asistencias
    public async Task<IActionResult> RegistrarLote(
        [FromBody] List<RegistrarAsistenciaDto> listaAsistencias)
    {
        try
        {
            var cantidad = await _asistenciaService.RegistrarLoteAsync(listaAsistencias);
            return Ok(new { Mensaje = $"Se procesaron {cantidad} registros correctamente." });
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar lote de asistencias.");
            return StatusCode(500, "Ocurrió un error interno al procesar el lote de asistencias.");
        }
    }


    [HttpPost("clase/estado")] // Se gestiona el estado de la clase (dictada o no)
    public async Task<IActionResult> ActualizarEstadoClase([FromBody] ClaseDictadaDTO dto)
    {
        try
        {
            await _asistenciaService.ActualizarEstadoClaseAsync(dto);

            string estado = dto.Dictada ? "Dictada" : "No Dictada";
            return Ok(new { Mensaje = $"La clase se marcó como '{estado}' correctamente y se recalcularon las asistencias." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar estado de la clase.");
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("estudiante/{estudianteId:guid}/dia/{fecha}")]
    public async Task<ActionResult<List<AsistenciaEspacioItemDto>>> GetEspaciosDia(
        Guid estudianteId, DateOnly fecha, CancellationToken ct = default)
    {
        try
        {
            var items = await _asistenciaService.ObtenerAsistenciaEspaciosDiaAsync(estudianteId, fecha, await GetIdDocenteAsync(ct));
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener espacios del día para estudiante {EstudianteId}", estudianteId);
            return StatusCode(500, "Error interno al obtener los datos.");
        }
    }

    [HttpGet("estudiante/{estudianteId:guid}/dia/{fecha}/auditoria-ec")]
    public async Task<ActionResult<List<AuditoriaAsistenciaECDto>>> GetAuditoriaEC(
        Guid estudianteId, DateOnly fecha)
    {
        try
        {
            var items = await _auditoriaECService.ObtenerPorEstudianteFechaAsync(estudianteId, fecha);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener auditoría EC para estudiante {EstudianteId}", estudianteId);
            return StatusCode(500, "Error interno al obtener los datos.");
        }
    }

    [HttpPut("espacio")]
    public async Task<IActionResult> ActualizarEspacio([FromBody] ActualizarAsistenciaEspacioDto dto, CancellationToken ct = default)
    {
        try
        {
            await _asistenciaService.ActualizarAsistenciaEspacioAsync(dto, await GetIdDocenteAsync(ct));
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar asistencia por espacio.");
            return StatusCode(500, ex.Message);
        }
    }

    private async Task<Guid?> GetIdDocenteAsync(CancellationToken ct = default)
    {
        var allClaims = User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
        _logger.LogInformation("[ACCESS] ALL claims en token: [{Claims}]", string.Join(" | ", allClaims));
        var roles = User.FindAll("roles").Select(c => c.Value).ToList();
        var esDocente = roles.Contains("Docente");
        _logger.LogInformation("[ACCESS] Roles en token: [{Roles}], esDocente={EsDocente}", string.Join(",", roles), esDocente);
        if (!esDocente) return null;
        var idUsuarioStr = User.FindFirstValue("idUsuario");
        _logger.LogInformation("[ACCESS] idUsuario del token: {IdUsuario}", idUsuarioStr);
        if (idUsuarioStr == null) return Guid.Empty;
        var idUsuario = Guid.Parse(idUsuarioStr);
        var docente = await _context.Docentes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario, ct);
        _logger.LogInformation("[ACCESS] Docente encontrado: {Resultado}, IdDocente: {IdDocente}", docente != null, docente?.IdDocente);
        return docente?.IdDocente ?? Guid.Empty;
    }
}
