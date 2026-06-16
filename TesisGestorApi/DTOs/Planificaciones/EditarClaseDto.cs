using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TesisGestorApi.DTOs.Planificaciones;

public class EditarClaseDto
{
    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    public string? FechaDesde { get; set; }
    public string? FechaHasta { get; set; }

    public string Estado { get; set; } = "PendienteDar";

    public Guid? IdBloqueTema { get; set; }

    public IFormFile? Archivo { get; set; }

    // Si es false y no viene nuevo archivo, se elimina la URL existente
    public bool MantieneArchivo { get; set; } = true;
}
