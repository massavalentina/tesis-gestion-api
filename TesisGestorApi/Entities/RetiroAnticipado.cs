using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class RetiroAnticipado
    {
        [Key]
        public Guid IdRetiro { get; set; }

        public DateTime HorarioRetiro { get; set; }
        public bool ConReingreso { get; set; }
        public DateTime? HorarioReingreso { get; set; } //Si no hay reingreso puede ser null 

        
        public Guid IdEstudiante { get; set; } //  Estudiante que se retira
        public Estudiante Estudiante { get; set; } = null!;

        
        public Guid IdTutor { get; set; } //  Tutor que autoriza
        public Tutor Tutor { get; set; } = null!;

        
        public Guid IdAsistencia { get; set; } //  Asistencia asociada
        public Asistencia Asistencia { get; set; } = null!;
    }

}
