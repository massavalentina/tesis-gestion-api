using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RepoDB.Entities
{
    public class Asistencia
    {
        [Key]
        public Guid Id { get; set; }
        public DateOnly Fecha { get; set; }
        public Guid EstudianteId { get; set; }
        public Estudiante Estudiante { get; set; } = null!;
        public Guid? TipoManianaId { get; set; } // El tipo de asistencia para el turno mañana
        [ForeignKey("TipoManianaId")]
        public TipoAsistencia? TipoManiana { get; set; }
        public TimeSpan? HoraEntradaManana { get; set; }
        public TimeSpan? HoraSalidaManana { get; set; }
        public TimeSpan? HoraEntradaTarde { get; set; }
        public TimeSpan? HoraSalidaTarde { get; set; }
        public Guid? TipoTardeId { get; set; } // El tipo de asistencia para el turno tarde
        [ForeignKey("TipoTardeId")]
        public TipoAsistencia? TipoTarde { get; set; }
        public decimal ValorTotalInasistencia { get; set; } = 0m; // Valor total por cálculo de asistencia a partir de los tipos de asistencia de los turnos

        /// Recalcula el valor total basándose en los estados actuales de Mañana y Tarde.
        public void CalcularAsistencia()
        {
            decimal total =
                (TipoManiana?.ValorBase ?? 0m) +
                (TipoTarde?.ValorBase ?? 0m);

            // Tope diario = 1.0
            ValorTotalInasistencia = Math.Min(total, 1.0m);
        }

    }
}