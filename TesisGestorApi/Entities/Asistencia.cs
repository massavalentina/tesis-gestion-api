using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Asistencia
    {
        [Key]
        public Guid IdAsistencia { get; set; }
        public DateTime FechaAsistencia { get; set; }

        public Turno Turno { get; set; }   // Mañana - Tarde 

        public Guid IdTipoAsistencia { get; set; }
        public TipoAsistencia TipoAsistencia { get; set; } = null!;  //Presente, Ausente, Tarde.

        public Guid IdEstudiante { get; set; } 
        public Estudiante Estudiante { get; set; } = null!;

        public RetiroAnticipado? RetiroAnticipado { get; set; }
    }


}
