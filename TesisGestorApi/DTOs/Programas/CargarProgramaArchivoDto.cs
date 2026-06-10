using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Programas;

public class CargarProgramaArchivoDto
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
    [Range(1, int.MaxValue, ErrorMessage = "Las horas cátedra deben ser al menos 1.")]
    public int HorasCatedra { get; set; }

    [Required]
    public IFormFile Archivo { get; set; } = null!;
}
