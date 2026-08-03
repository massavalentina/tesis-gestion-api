using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Interfaces;

[Authorize]
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
        var tokens = QuitarTildes(texto)
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var hoy = DateOnly.FromDateTime(DateTime.Now);

        var candidatos = await (
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
        .ToListAsync();

        // Filtro insensible a tildes/mayúsculas y por palabras: se hace en memoria
        // porque ILIKE de Postgres no ignora acentos (García no matcheaba "garcia").
        // Cada palabra tipeada debe matchear nombre, apellido o documento —
        // así "acosta mia" (o "Acosta, Mia", con o sin coma) encuentra al alumno,
        // sin importar el orden apellido/nombre.
        var resultados = candidatos
            .Where(x => tokens.All(token =>
                QuitarTildes(x.Nombre).Contains(token, StringComparison.OrdinalIgnoreCase)
                || QuitarTildes(x.Apellido).Contains(token, StringComparison.OrdinalIgnoreCase)
                || x.Documento.Contains(token)))
            .OrderBy(x => x.Apellido)
            .ThenBy(x => x.Nombre)
            .Take(10)
            .ToList();

        return Ok(resultados);
    }

    private static string QuitarTildes(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sinTildes = new StringBuilder();
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sinTildes.Append(c);
        }
        return sinTildes.ToString().Normalize(NormalizationForm.FormC);
    }
}