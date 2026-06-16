using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Planificaciones;

public class CambiarEstadoClaseDto
{
    [Required]
    public string Estado { get; set; } = null!;
}
