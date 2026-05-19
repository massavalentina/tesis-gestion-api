using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Asignaciones
{
    public class AsignarCursoDto
    {
        [Required]
        public Guid IdCurso { get; set; }
    }
}
