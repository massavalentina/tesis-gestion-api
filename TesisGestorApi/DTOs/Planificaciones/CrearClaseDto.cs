using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TesisGestorApi.DTOs.Planificaciones;

public class CrearClaseDto
{
    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    // "yyyy-MM-dd" o null
    public string? FechaDesde { get; set; }
    public string? FechaHasta { get; set; }

    public string Estado { get; set; } = "PendienteDar";

    // Bloque del tema al que se vincula (opcional)
    public Guid? IdBloqueTema { get; set; }

    public IFormFile? Archivo { get; set; }
}
