namespace TesisGestorApi.DTOs.Programas;

public class ProgramaDetalleDto
{
    public Guid IdPrograma { get; set; }
    public Guid IdCurso { get; set; }
    public Guid IdEC { get; set; }
    public int AnioLectivo { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int HorasCatedra { get; set; }
    public string Estado { get; set; } = null!;
    public string Origen { get; set; } = null!;
    public string? Url { get; set; }
    public string FechaVencimiento { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public string NombreMateria { get; set; } = null!;
    public string CodigoCurso { get; set; } = null!;
    public int AnioNumero { get; set; }
    public string Division { get; set; } = null!;
    public string NombreDocente { get; set; } = null!;
    public List<ObjetivoDetalleDto> Objetivos { get; set; } = new();
    public List<UnidadDetalleDto> Unidades { get; set; } = new();
}

public class ObjetivoDetalleDto
{
    public Guid IdObjetivo { get; set; }
    public string Descripcion { get; set; } = null!;
    public int Nro { get; set; }
}

public class UnidadDetalleDto
{
    public Guid IdUnidad { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Nro { get; set; }
    public List<TemaDetalleDto> Temas { get; set; } = new();
}

public class TemaDetalleDto
{
    public Guid IdTema { get; set; }
    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Nro { get; set; }
}
