using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class TutorEstudiante
    {
       
        public Guid IdTutor { get; set; }
        public Tutor Tutor { get; set; } = null!;

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

    }
}
