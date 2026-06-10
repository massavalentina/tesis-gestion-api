using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Unidad
    {
        [Key]
        public Guid IdUnidad { get; set; }

        public Guid IdPrograma { get; set; }
        public Programa Programa { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string Titulo { get; set; } = null!;

        [MaxLength(2000)]
        public string? Descripcion { get; set; }

        public int Nro { get; set; }

        public ICollection<Tema> Temas { get; set; } = new List<Tema>();
    }
}
