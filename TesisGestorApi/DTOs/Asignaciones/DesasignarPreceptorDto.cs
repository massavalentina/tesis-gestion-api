using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Asignaciones
{
    public class DesasignarPreceptorDto
    {
        [Required]
        public string Motivo { get; set; } = null!;
    }
}
