using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepoDB.Entities;
using System.Runtime.InteropServices.Marshalling;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;
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

    [HttpGet] // Peticiones Get All
    public async Task<ActionResult<IEnumerable<AsistenciaGetDTO>>> GetAsistencias(
        [FromQuery] DateTime? fecha,
        [FromQuery] Guid? estudianteId)
    {
        var query = _context.Asistencias
            .Include(a => a.Estudiante)
            .Include(a => a.TipoManiana)
            .Include(a => a.TipoTarde)
            .AsNoTracking()
            .AsQueryable();

        if (fecha.HasValue) { query = query.Where(a => a.Fecha.Date == fecha.Value.Date); } // Si viene fecha, la query suma fecha

        if (estudianteId.HasValue) { query = query.Where(a => a.EstudianteId == estudianteId.Value); } // Si viene estudiante, la query filtra por estudiante también

        var resultados = await query
            .Select(a => new AsistenciaGetDTO
            {
                Id = a.Id,
                Fecha = a.Fecha,
                ValorTotal = a.ValorTotalInasistencia,
                NombreCompleto = $"{a.Estudiante.Nombre} {a.Estudiante.Apellido}",
                Documento = a.Estudiante.Documento,
                CodigoManana = a.TipoManiana != null ? a.TipoManiana.Codigo : "-",
                CodigoTarde = a.TipoTarde != null ? a.TipoTarde.Codigo : "-"
            })
            .OrderByDescending(a => a.Fecha) // Opcional: ordenar por fecha
            .ToListAsync();

        return Ok(resultados);
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
}
