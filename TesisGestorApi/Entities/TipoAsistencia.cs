using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class TipoAsistencia
    {
        [Key]
        public Guid IdTipo { get; set; }

        [Required]
        [MaxLength(10)]
        public string Codigo { get; set; } // P, A, LLT, LLTE, LLTC
        public string Descripcion { get; set; } = null!; // Presente - Ausente - Llegada Tarde - Llegada Tarde Extendida - Llegada Tarde Completa
        public decimal ValorBase { get; set; } // Valor numérico asociado a este tipo de asistencia (0, 0.25, 0.5, 1)

    }
}

