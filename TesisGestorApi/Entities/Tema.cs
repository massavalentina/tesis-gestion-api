using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Tema
    {
        [Key]
        public Guid IdTema { get; set; }

        public Guid IdUnidad { get; set; }
        public Unidad Unidad { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string Titulo { get; set; } = null!;

        [MaxLength(2000)]
        public string? Descripcion { get; set; }

        public int Nro { get; set; }
    }
}
