using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class DetalleCursado
    {
        [Key]
        public Guid IdCursado { get; set; }

        
        public bool Estado { get; set; } // Indica si el estudiante está activo en el curso

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;
    }

}
