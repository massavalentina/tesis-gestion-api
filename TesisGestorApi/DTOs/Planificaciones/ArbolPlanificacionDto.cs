namespace TesisGestorApi.DTOs.Planificaciones;

public class ArbolPlanificacionDto
{
    // Estados de bloqueo (se devuelven con el resto vacío)
    public bool Bloqueado { get; set; } = false;
    public string? MensajeBloqueo { get; set; }
    public bool SinPrograma { get; set; } = false;

    public bool PermiteCrearItems { get; set; }
    public string NombreMateria { get; set; } = null!;
    public string TituloPrograma { get; set; } = null!;
    public string NombreDocente { get; set; } = null!;
    public int AnioLectivo { get; set; }
    public string EstadoPrograma { get; set; } = null!;
    public string? UrlPrograma { get; set; }
    public double Avance { get; set; }
    public int TotalTemas { get; set; }
    public int TemasCompletos { get; set; }
    public List<UnidadArbolDto> Unidades { get; set; } = new();
}

public class UnidadArbolDto
{
    public Guid IdUnidad { get; set; }
    public Guid IdBloquePrograma { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Nro { get; set; }
    public string Estado { get; set; } = null!;
    public List<TemaArbolDto> Temas { get; set; } = new();
}

public class TemaArbolDto
{
    public Guid IdTema { get; set; }
    public Guid IdBloquePrograma { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Nro { get; set; }
    public string Estado { get; set; } = null!;
    public List<ClasePlanificacionDto> Clases { get; set; } = new();
}

public class ClasePlanificacionDto
{
    public Guid IdPlanificacion { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? FechaEstimada { get; set; }
    public string? FechaDictada { get; set; }
    public string Estado { get; set; } = null!;
    public string? Url { get; set; }
    public DateTime FechaCreacion { get; set; }
}
