using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RepoDB.Entities
{
    public class Asistencia
    {
        [Key]
        public Guid Id { get; set; }
        public DateTime Fecha { get; set; }
        public Guid EstudianteId { get; set; }
        public Estudiante Estudiante { get; set; } = null!;
        public Guid? TipoManianaId { get; set; } // El tipo de asistencia para el turno mañana
        [ForeignKey("TipoManianaId")]
        public TipoAsistencia? TipoManiana { get; set; }
        public Guid? TipoTardeId { get; set; } // El tipo de asistencia para el turno tarde
        [ForeignKey("TipoTardeId")]
        public TipoAsistencia? TipoTarde { get; set; } 
        public decimal ValorTotalInasistencia { get; set; } // Valor total por cálculo de asistencia a partir de los tipos de asistencia de los turnos

        /// Recalcula el valor total basándose en los estados actuales de Mañana y Tarde.
        public void CalcularAsistencia()
        {
            decimal total = 0m;

            // Turno Mañana
            if (TipoManiana != null)
            {
                switch (TipoManiana.Codigo)
                {
                    case "A": // Ausente Mañana = Inasistencia Completa 
                        ValorTotalInasistencia = 1.0m;
                        return; 
                    case "LLT":
                        total += 0.25m; // Llegada Tarde = 1/4 falta
                        break;
                    case "LLTE":
                        total += 0.5m; // Llegada Tarde Extendida = 1/2 falta
                        break;
                    case "LLTC":
                        total += 1.0m; // Llegada Tarde Completa = 1 falta
                        break;
                    case "RA": 
                        total += 0.5m; // Retiro Anticipado  = 1/2 falta
                        break;
                    case "RAE": 
                        ValorTotalInasistencia = 1.0m; // Retiro Anticipado Extendido = 1 falta
                        break;

                    default: // Presente (P) - Ausente No Computado (ANC) - Retiro Anticipado Express (RE)
                        total += 0.0m;
                        break;
                }
            }

            // Turno Tarde
            if (TipoTarde != null)
            {
                if (TipoTarde.Codigo.ToUpper() == "A") // Ausente Tarde
                {
                    total += 0.5m;
                }
            }

            // 3. ASIGNACIÓN FINAL (Tope de 1)
            ValorTotalInasistencia = total > 1.5m ? 1.5m : total;
        }
    }

}