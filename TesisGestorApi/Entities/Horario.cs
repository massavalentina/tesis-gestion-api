using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Horario
    {
        [Key]
        public Guid IdHorario { get; set; }

        public DateTime HorarioEntrada { get; set; }
        public DateTime HorarioSalida { get; set; }

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public ICollection<Curricula> Curriculas { get; set; }
            = new List<Curricula>();
    }

}
