using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class AsistenciaPorEspacio
    {
        [Key]
        public Guid IdAsistenciaEspacio { get; set; }

        public DateOnly Fecha { get; set; }

        
        public Guid IdEstudiante { get; set; }  // A que estudiante pertenece
        public Estudiante Estudiante { get; set; } = null!;

        public Guid IdClaseDictada { get; set; } // Clase dictada ese día para ese espacio curricular
        public ClaseDictada ClaseDictada { get; set; }
        public bool Presente { get; set; }
        public string Motivo { get; set; }
    }

}


//  Asistencia general con horario de ingreso definido
// La Asistencia por Espacio tiene que estar linkeada al horario que tiene esa currícula
// Entonces según el día, la asistencia general va a completar la asistencia a esa currícula del estudiante
// Siempre cuando esté presente. En caso de retiro anticipado, la asistencia por espacio debe contemplar la posible ausencia de acuerdo al
// horario de retiro. 