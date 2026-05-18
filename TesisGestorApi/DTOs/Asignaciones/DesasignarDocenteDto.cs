using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Asignaciones
{
    public class DesasignarDocenteDto
    {
        [Required]
        public string Motivo { get; set; } = null!;
    }
}
