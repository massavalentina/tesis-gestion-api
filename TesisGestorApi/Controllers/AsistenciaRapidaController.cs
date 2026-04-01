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

    [HttpGet("servertime")]
    public ActionResult GetServerTime()
    {
        var now = DateTime.Now;
        return Ok(new
        {
            fecha = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            hora = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
        });
    }

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
            _logger.LogError(ex, "Error asistencia rápida");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("deshacer")]
    public async Task<ActionResult<AsistenciaResponseDto>> Deshacer(
        [FromBody] DeshacerAsistenciaRapidaDto dto)
    {
        try
        {
            var resultado = await _asistenciaService.DeshacerAsistenciaRapidaAsync(dto);
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al deshacer asistencia");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("buscar-estudiantes")]
    public async Task<ActionResult<IEnumerable<EstudianteBusquedaRapidaDto>>> BuscarEstudiantes([FromQuery] string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3)
            return Ok(new List<EstudianteBusquedaRapidaDto>());

        texto = texto.Trim();
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        var resultados = await (
            from e in _context.Estudiantes.AsNoTracking()

                // Detalle cursado activo
            join dc in _context.DetallesCursado
                .AsNoTracking()
                .Where(x => x.Estado)
                on e.IdEstudiante equals dc.IdEstudiante into dcJoin
            from dc in dcJoin.DefaultIfEmpty()

                // Curso
            join c in _context.Cursos
                .AsNoTracking()
                on dc.IdCurso equals c.IdCurso into cJoin
            from c in cJoin.DefaultIfEmpty()

                // Asistencia del día
            join a in _context.Asistencias
                .AsNoTracking()
                .Where(x => x.Fecha == hoy)
                on e.IdEstudiante equals a.EstudianteId into asistHoy
            from ah in asistHoy.DefaultIfEmpty()

                // Tipo mañana
            join tm in _context.TiposAsistencia
                .AsNoTracking()
                on ah.TipoManianaId equals tm.IdTipo into tmJoin
            from tm in tmJoin.DefaultIfEmpty()

            where EF.Functions.ILike(e.Nombre, $"%{texto}%")
               || EF.Functions.ILike(e.Apellido, $"%{texto}%")
               || e.Documento.Contains(texto)

            select new EstudianteBusquedaRapidaDto
            {
                Id = e.IdEstudiante,
                Nombre = e.Nombre,
                Apellido = e.Apellido,
                Documento = e.Documento,
                Curso = c != null ? c.Codigo : "-",
                RegistradoHoy = (tm != null) && (
                    tm.Codigo.ToUpper() == "LLT" ||
                    tm.Codigo.ToUpper() == "LLTE" ||
                    tm.Codigo.ToUpper() == "LLTC"
                ),
                TeaGeneral = e.TeaGeneral
            }
        )
        .OrderBy(x => x.Apellido)
        .ThenBy(x => x.Nombre)
        .Take(10)
        .ToListAsync();

        return Ok(resultados);
    }
}