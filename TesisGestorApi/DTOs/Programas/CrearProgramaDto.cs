using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Programas;

public class CrearProgramaDto
{
    [Required]
    public Guid IdCurso { get; set; }

    [Required]
    public Guid IdEC { get; set; }

    [Required]
    public int AnioLectivo { get; set; }

    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int HorasCatedra { get; set; }

    [Required]
    [MinLength(1)]
    public List<ObjetivoDto> Objetivos { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<UnidadDto> Unidades { get; set; } = new();
}

public class ObjetivoDto
{
    [Required]
    [MaxLength(1000)]
    public string Descripcion { get; set; } = null!;

    public int Nro { get; set; }
}

public class UnidadDto
{
    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    public int Nro { get; set; }

    [Required]
    [MinLength(1)]
    public List<TemaDto> Temas { get; set; } = new();
}

public class TemaDto
{
    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    public int Nro { get; set; }
}

public class CambiarEstadoProgramaDto
{
    [Required]
    public string Estado { get; set; } = null!;
}
