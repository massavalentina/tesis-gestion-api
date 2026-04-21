using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TesisGestorApi.Entities
{
    public class Asistencia
    {
        [Key]
        public Guid Id { get; set; }
        public DateOnly Fecha { get; set; }
        public Guid EstudianteId { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        // ── Turno Mañana ──────────────────────────────────────────────────────
        // TipoManianaId almacena el estado más reciente del turno. Si hay un retiro contemplará el retiro como último estado.
        public Guid? TipoManianaId { get; set; }
        [ForeignKey("TipoManianaId")]
        public TipoAsistencia? TipoManiana { get; set; }

        // TipoLlegadaManianaId almacena el código de llegada (Presente, Ausente o Llegada Tarde)
        // Aunque TipoManianaId se sobreescriba, este campo mantendrá el primer código.
        public Guid? TipoLlegadaManianaId { get; set; }
        [ForeignKey("TipoLlegadaManianaId")]
        public TipoAsistencia? TipoLlegadaManiana { get; set; }

        public TimeSpan? HoraEntradaManana { get; set; }
        public TimeSpan? HoraSalidaManana { get; set; }

        // ── Turno Tarde ───────────────────────────────────────────────────────
        public Guid? TipoTardeId { get; set; }
        [ForeignKey("TipoTardeId")]
        public TipoAsistencia? TipoTarde { get; set; }

        public TimeSpan? HoraEntradaTarde { get; set; }
        public TimeSpan? HoraSalidaTarde { get; set; }

        // Valor total de inasistencia del día. El rango contemplado es [ 0.0, 0.25, 0.5, 0.75, 1.0].
        public decimal ValorTotalInasistencia { get; set; } = 0m;

        /// Este método recalcula el valor de la inasistencia a partir del tipo de cada turno 
        /// y del tiempo de clases asistido sobre el total dado.
        
        /// Parámetros (turno mañana):
        ///   minTotalesM = minutos totales de clases dadas en el turno.
        ///   minPerdidaIngresoM = minutos perdidos por llegada tarde.
        ///   minPerdidaSalidaM  = minutos perdidos por retiro anticipado en mañana
        
        /// Parámetros (turno tarde):
        ///   minTotalesT, minPerdidaIngresoT, minPerdidaSalidaT

        public void CalcularAsistencia(
            double minTotalesM, double minPerdidaIngresoM, double minPerdidaSalidaM,
            double minTotalesT, double minPerdidaIngresoT, double minPerdidaSalidaT,
            bool procesadoManana = true, bool procesadoTarde = true)
        {
            decimal valorM = 0m;
            decimal valorT = 0m;

            // ── TURNO MAÑANA ─────────────────────────────────────────────────
            // Utiliza TipoLlegadaManiana cuando existe sino utiliza TipoManiana
            // TipoManiana puede ser sobreescrito por un retiro posterior.
            // Si no hay retiro, ambas propiedades apuntan lo mismo.
            var tipoLlegadaM = TipoLlegadaManiana ?? TipoManiana;

            if (tipoLlegadaM != null)
            {
                // Valores de llegada
                decimal valorLlegada = tipoLlegadaM.Codigo.ToUpper() switch
                {
                    "LLT"  => 0.25m,  // Llegada Tarde (Primeros 10 minutos)
                    "LLTE" => 0.50m,  // Llegada Tarde Extendida (Entre 10 y 25 minutos)
                    "LLTC" => 1.00m,  // Llegada Tarde Completa (+ 25 minutos)
                    "A"    => 1.00m,  // Ausente
                    "RAE"  => 1.00m,  // Retiro Anticipado (sin llegada previa registrada)
                    _      => 0.00m   // Presente (P), ANC (Ausente No Computable), RE (Retiro Express), RA (Retiro Anticipado)
                };

                // Valor por retiro (calculado en base al porcentaje de tiempo perdido al final del turno, sobre clases efectivamente dadas)
                decimal valorRetiro = 0m;

                // Se calcula el retiro si hay minutos de clases dadas y la llegada no es completa (ya es una inasistencia total).
                if (minTotalesM > 0 && valorLlegada < 1.0m)
                {
                    double porcPerdidaSalida = (minPerdidaSalidaM / minTotalesM) * 100.0;

                    if      (porcPerdidaSalida > 50) valorRetiro = 1.0m;  // RAE
                    else if (porcPerdidaSalida > 10) valorRetiro = 0.5m;  // RA
                    // <= 10%: Retiro Express (RE), no genera inasistencia
                }
                else if (!procesadoManana && valorLlegada < 1.0m && TipoManiana != null)
                {
                    // Solo se procesó el turno tarde: los ECs de mañana fueron salteados (minTotalesM = 0).
                    // Se preserva el valor del retiro usando directamente el código de TipoManiana.
                    valorRetiro = TipoManiana.Codigo.ToUpper() switch
                    {
                        "RAE" => 1.0m,
                        "RA"  => 0.5m,
                        _     => 0.0m   // RE y otros no generan inasistencia
                    };
                }

                // Suma de ambos componentes con un tope de 1.0 para la mañana
                valorM = Math.Min(1.0m, valorLlegada + valorRetiro);
            }

            // ── TURNO TARDE ──────────────────────────────────────────────────
            // En el turno tarde la inasistencia máxima es de 0.5. Si hay ausencia, llegada tarde o retiro, corresponde 0.5.
            // La presencia o el retiro express no computan inasistencia.
            if (TipoTarde != null)
            {
                valorT = TipoTarde.Codigo.ToUpper() switch
                {
                    "P"   => 0.0m,  // Presente
                    "RE"  => 0.0m,  // Retiro Express (≤ 10% perdido)
                    "ANC" => 0.0m,  // Ausencia No Computable
                    _     => 0.5m   // A, LLT, LLTE, LLTC, RA, RAE => media inasistencia
                };
            }

            // ── TOTAL FINAL ──────────────────────────────────────────────────
            // El tope diario de inasistencia es de 1 (falta completa), aunque la suma de turnos supere ese valor.
            ValorTotalInasistencia = Math.Min(1.0m, valorM + valorT);
        }
    }
}
