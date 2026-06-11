using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class EspacioCurricular
    {
        [Key]
        public Guid IdEC { get; set; }

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        // Docente titular (nullable: puede no tener uno asignado aún)
        public Guid? IdDocente { get; set; }
        public Docente? Docente { get; set; }

        public Guid IdCurricula { get; set; }
        public Curricula Curricula { get; set; } = null!;

        public ICollection<Horario> Horarios { get; set; } = new List<Horario>();
        public ICollection<ClaseDictada> ClasesDictadas { get; set; } = new List<ClaseDictada>();
        public ICollection<InstanciaEvaluativa> InstanciasEvaluativas { get; set; } = new List<InstanciaEvaluativa>();
        public ICollection<DocenteEspacioCurricular> DocentesEspaciosCurriculares { get; set; }
            = new List<DocenteEspacioCurricular>();
    }
}
