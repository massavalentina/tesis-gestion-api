using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Curricula
    {
        [Key]
        public Guid IdCurricula { get; set; }

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public Guid IdHorario { get; set; }
        public Horario Horario { get; set; } = null!;

        public Guid IdDocente { get; set; }
        public Docente Docente { get; set; } = null!;

        public Guid IdEspacioCurricular { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public ICollection<AsistenciaPorEspacio> AsistenciasPorEspacio { get; set; }
            = new List<AsistenciaPorEspacio>();
    }


}
