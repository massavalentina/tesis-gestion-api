using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Horario
    {
        [Key]
        public Guid IdHorario { get; set; }
        public DayOfWeek DíaSemana { get; set; } // Día de la semana del horario
        public TimeSpan HorarioEntrada { get; set; }
        public TimeSpan HorarioSalida { get; set; }

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;

        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; }
    }

}
