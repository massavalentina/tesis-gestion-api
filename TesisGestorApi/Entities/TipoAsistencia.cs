using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class TipoAsistencia
    {
        [Key]
        public Guid IdTipoAsistencia { get; set; }

        [Required]
        public string Codigo { get; set; } = null!;

        [Required]
        public string Descripcion { get; set; } = null!;

        public decimal Valor { get; set; }   // 0, 0.25, 0.5, 1
    }
}

