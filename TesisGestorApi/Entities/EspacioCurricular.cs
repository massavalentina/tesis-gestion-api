using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class EspacioCurricular
    {
        [Key]
        public Guid IdEC { get; set; }

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;
        public Guid IdDocente { get; set; }
        public Docente Docente { get; set; } = null!;

        public Guid IdCurricula { get; set; }
        public Curricula Curricula { get; set; } = null!;

        public ICollection<Horario> Horarios { get; set; } = new List<Horario>();
        public ICollection<ClaseDictada> ClasesDictadas { get; set; } = new List<ClaseDictada>();
    }


}
