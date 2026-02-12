using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;

[ApiController]
[Route("api/asistencia-rapida")]
public class AsistenciaRapidaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAsistenciaService _asistenciaService;
    private readonly ILogger<AsistenciaRapidaController> _logger;

    public AsistenciaRapidaController(
        ApplicationDbContext context,
        IAsistenciaService asistenciaService,
        ILogger<AsistenciaRapidaController> logger)
    {
        _context = context;
        _asistenciaService = asistenciaService;
        _logger = logger;
    }

    // GET /api/asistencia-rapida/tipos
    [HttpGet("tipos")]
    public async Task<ActionResult<IEnumerable<TipoAsistenciaRapidaDTO>>> GetTipos()
    {
        var tipos = await _context.TiposAsistencia
            .AsNoTracking()
            .OrderBy(t => t.Codigo)
            .Select(t => new TipoAsistenciaRapidaDTO
            {
                Id = t.IdTipo,
                Codigo = t.Codigo,
                Descripcion = t.Descripcion
            })
            .ToListAsync();

        return Ok(tipos);
    }

    // ✅ GET /api/asistencia-rapida/servertime
    // Para mostrar en el modal (hora/fecha exacta del servidor).
    [HttpGet("servertime")]
    public ActionResult GetServerTime()
    {
        var now = DateTime.Now; // hora del servidor
        return Ok(new
        {
            fecha = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            hora = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
        });
    }

    // POST /api/asistencia-rapida
    [HttpPost]
    public async Task<ActionResult<AsistenciaResponseDto>> RegistrarAsistenciaRapida(
        [FromBody] RegistrarAsistenciaDto request)
    {
        try
        {
            var resultado = await _asistenciaService.RegistrarAsistenciaIndividualAsync(request);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error asistencia rápida. EstudianteId={EstudianteId}", request.EstudianteId);
            return BadRequest(ex.Message);
        }
    }

    // GET /api/asistencia-rapida/buscar-estudiantes?texto=...
    [HttpGet("buscar-estudiantes")]
    public async Task<ActionResult<IEnumerable<EstudianteBusquedaRapidaDto>>> BuscarEstudiantes([FromQuery] string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
            return Ok(new List<EstudianteBusquedaRapidaDto>());

        texto = texto.Trim();

        // Query base
        var estudiantesQuery = _context.Estudiantes.AsNoTracking().AsQueryable();

        // Si es numérico => Documento/DNI
        bool esNumero = texto.All(char.IsDigit);

        if (esNumero)
        {
            estudiantesQuery = estudiantesQuery.Where(e => e.Documento.Contains(texto));
        }
        else
        {
            estudiantesQuery = estudiantesQuery.Where(e =>
                EF.Functions.ILike(e.Nombre, $"%{texto}%") ||
                EF.Functions.ILike(e.Apellido, $"%{texto}%")
            );
        }

        // ✅ Hoy con DateOnly (no DateTime)
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        // ✅ RegistradoHoy = existe asistencia hoy (día)
        var resultados = await (
            from e in estudiantesQuery
            join a in _context.Asistencias.AsNoTracking()
                  .Where(x => x.Fecha == hoy)
                on e.IdEstudiante equals a.EstudianteId into asistHoy
            from ah in asistHoy.DefaultIfEmpty()
            select new EstudianteBusquedaRapidaDto
            {
                Id = e.IdEstudiante,
                Nombre = e.Nombre,
                Apellido = e.Apellido,
                Documento = e.Documento,
                Curso = "-",
                RegistradoHoy = (ah != null)
            }
        )
        .OrderBy(x => x.Apellido).ThenBy(x => x.Nombre)
        .Take(10)
        .ToListAsync();

        return Ok(resultados);
    }
}

