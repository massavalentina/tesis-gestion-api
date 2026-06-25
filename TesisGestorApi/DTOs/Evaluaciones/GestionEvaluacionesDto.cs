using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using TesisGestorApi.DTOs.Planificaciones;

namespace TesisGestorApi.DTOs.Evaluaciones;

public class GestionEvaluacionesDto
{
    public bool SinPrograma { get; set; }
    public bool TrazabilidadDisponible { get; set; }
    public string? MensajeTrazabilidad { get; set; }

    public Guid? IdPrograma { get; set; }
    public string? TituloPrograma { get; set; }
    public string? EstadoPrograma { get; set; }
    public string? OrigenPrograma { get; set; }

    public string NombreMateria { get; set; } = null!;
    public string NombreDocente { get; set; } = null!;
    public string NombreCurso { get; set; } = null!;
    public int AnioLectivo { get; set; }

    public List<UnidadArbolDto> Unidades { get; set; } = new();
    public List<InstanciaEvaluativaSlotDto> Instancias { get; set; } = new();
}

public class InstanciaEvaluativaSlotDto
{
    public Guid? IdIE { get; set; }
    public int Nro { get; set; }
    public bool Existe { get; set; }
    public string Estado { get; set; } = "SinCarga";

    public ArchivoIETrazadoDto? NotaOriginal { get; set; }
    public ArchivoIETrazadoDto? Recuperatorio1 { get; set; }
    public ArchivoIETrazadoDto? Recuperatorio2 { get; set; }
}

public class ArchivoIETrazadoDto
{
    public Guid IdArchivoIE { get; set; }
    public string TipoCalificacion { get; set; } = null!;
    public string TipoIE { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string NombreArchivo { get; set; } = null!;
    public string? UrlArchivo { get; set; }
    public DateTime FechaEjecucion { get; set; }
    public DateTime FechaCarga { get; set; }
    public bool TieneCalificaciones { get; set; }
    public bool PuedeEditar { get; set; }
    public bool PuedeEliminar { get; set; }
    public string? MotivoBloqueo { get; set; }
    public List<Guid> IdBloquesTema { get; set; } = new();
}

public class GuardarArchivoIEFormDto
{
    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [Required]
    public string TipoIE { get; set; } = null!;

    [Required]
    public string FechaEjecucion { get; set; } = null!;

    [Required]
    public string Estado { get; set; } = "Pendiente";

    public List<Guid> IdBloquesTema { get; set; } = new();

    public IFormFile? Archivo { get; set; }
}

public class CambiarEstadoIEFormDto
{
    [Required]
    public string Estado { get; set; } = "Pendiente";
}
