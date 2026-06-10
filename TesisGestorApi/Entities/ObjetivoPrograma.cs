using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class ObjetivoPrograma
    {
        [Key]
        public Guid IdObjetivo { get; set; }

        public Guid IdPrograma { get; set; }
        public Programa Programa { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Descripcion { get; set; } = null!;

        public int Nro { get; set; }
    }
}
