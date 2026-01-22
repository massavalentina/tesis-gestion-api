using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class AsistenciaPorEspacio
    {
        [Key]
        public Guid IdAsistenciaEspacio { get; set; }

        public DateTime FechaAsistencia { get; set; }

        
        public Guid IdEstudiante { get; set; }  // A que estudiante pertenece
        public Estudiante Estudiante { get; set; } = null!;

        
        public Guid IdCurricula { get; set; } // Espacio curricular + docente
        public Curricula Curricula { get; set; } = null!; 

        
        public Guid IdTipoAsistencia { get; set; } // Tipo de asistencia (Presente, Ausente, Tarde, etc.)
        public TipoAsistencia TipoAsistencia { get; set; } = null!;
    }

}
