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

        /// Recalcula el valor total basándose en los estados actuales de Mañana y Tarde,
        /// así como el tiempo de asistencia total en ambos turnos
        public void CalcularAsistencia(
            double minTotalesM, double minPerdidaIngresoM, double minPerdidaSalidaM,
            double minTotalesT, double minPerdidaIngresoT, double minPerdidaSalidaT)
        {
            decimal valorM = 0m;
            decimal valorT = 0m;

            // --- TURNO MAÑANA ---
            if (TipoManiana != null)
            {
                // 1. Valor por LLEGADA (Según código manual del preceptor)
                decimal valorLlegada = TipoManiana.Codigo.ToUpper() switch
                {
                    "LLT" => 0.25m,
                    "LLTE" => 0.50m,
                    "LLTC" => 1.00m,
                    "A" => 1.00m, // Si puso Ausente manual, ya es 1.0
                    "RAE" => 1.00m, // RAE manual pisa todo
                    _ => 0.00m
                };

                // 2. Valor por RETIRO (Automático según tiempo perdido AL FINAL)
                decimal valorRetiro = 0m;

                // Solo calculamos retiro si NO es un código de inasistencia total manual
                if (minTotalesM > 0 && valorLlegada < 1.0m)
                {
                    double porcPerdidaSalida = (minPerdidaSalidaM / minTotalesM) * 100.0;

                    if (porcPerdidaSalida > 50) valorRetiro = 1.0m; // RAE Automático
                    else if (porcPerdidaSalida > 10) valorRetiro = 0.5m; // RA Automático
                                                                         // Si es <= 10% se considera RE (Express) y no suma
                }

                // 3. Suma inteligente
                valorM = Math.Min(1.0m, valorLlegada + valorRetiro);
            }

            // --- TURNO TARDE ---
            if (TipoTarde != null)
            {
                if (TipoTarde.Codigo.ToUpper() == "A")
                {
                    valorT = 0.5m;
                }
                else if (minTotalesT > 0)
                {
                    // En la tarde, miramos la pérdida global (ingreso + salida) o solo salida
                    // según tu regla. Generalmente Retiro Tarde = Media Falta.
                    double porcPerdidaSalida = (minPerdidaSalidaT / minTotalesT) * 100.0;

                    if (porcPerdidaSalida > 10) valorT = 0.5m;
                }
            }

            // --- TOTAL FINAL ---
            ValorTotalInasistencia = Math.Min(1.5m, valorM + valorT);
        }

    }
}