using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Planificaciones;

public class CrearItemArchivoDto
{
    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }
}
