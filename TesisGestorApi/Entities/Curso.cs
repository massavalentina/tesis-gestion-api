using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Curso
    {
        [Key]
        public Guid IdCurso { get; set; }

        public string Codigo { get; set; } = null!;  //Union entre Año y Division
        public bool Estado { get; set; }
        public DateTime AñoLectivo { get; set; }

        public Guid IdAnio { get; set; }
        public Anio Anio { get; set; } = null!;

        public Guid IdDivision { get; set; }
        public Division Division { get; set; } = null!;

        
        public ICollection<DetalleCursado> DetallesCursado { get; set; } // Estudiantes de ese curso
            = new List<DetalleCursado>();

       
        public ICollection<Curricula> Curriculas { get; set; }  // Materias del curso
            = new List<Curricula>();

        
        public ICollection<Horario> Horarios { get; set; } // Horarios del curso
            = new List<Horario>();

        
        public ICollection<Preceptor> Preceptores { get; set; } // Preceptores asignados
            = new List<Preceptor>();
    }

}
