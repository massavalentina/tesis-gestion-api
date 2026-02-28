using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using System.Runtime.InteropServices.Marshalling;
using TesisGestorApi.Data;
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
}
