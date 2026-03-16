using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using System.Runtime.InteropServices.Marshalling;
using TesisGestorApi.Data;
using TesisGestorApi.Dtos;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;
using TesisGestorApi.Services;


[ApiController]
[Route("api/[controller]")]

public class AsistenciaController : ControllerBase
{
    private IAsistenciaService _asistenciaService; // Servicio para lógica de negocio (cálculo de asistencias)
    private readonly ApplicationDbContext _context; // Conexión a Db
    private readonly ILogger<AsistenciaController> _logger; // Logger de errores

    public AsistenciaController(ApplicationDbContext context, ILogger<AsistenciaController> logger, IAsistenciaService asistenciaService)
    {
        _context = context; // Inyección de Dependencias
        _logger = logger;
        _asistenciaService = asistenciaService;
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
    public async Task<ActionResult<List<OpcionSeleccionDto>>> GetCursos()
    {
        try
        {
            var cursos = await _asistenciaService.ObtenerCursosAsync();
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
        Guid estudianteId, DateOnly fecha)
    {
        try
        {
            var items = await _asistenciaService.ObtenerAsistenciaEspaciosDiaAsync(estudianteId, fecha);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener espacios del día para estudiante {EstudianteId}", estudianteId);
            return StatusCode(500, "Error interno al obtener los datos.");
        }
    }

    [HttpPut("espacio")]
    public async Task<IActionResult> ActualizarEspacio([FromBody] ActualizarAsistenciaEspacioDto dto)
    {
        try
        {
            await _asistenciaService.ActualizarAsistenciaEspacioAsync(dto);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar asistencia por espacio.");
            return StatusCode(500, ex.Message);
        }
    }
}
