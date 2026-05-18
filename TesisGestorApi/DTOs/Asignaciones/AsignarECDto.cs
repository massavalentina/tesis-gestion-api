using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Asignaciones
{
    public class AsignarECDto
    {
        [Required]
        public Guid IdEC { get; set; }
    }
}
