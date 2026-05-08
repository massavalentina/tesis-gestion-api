using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Preceptor
    {
        [Key]
        public Guid IdPreceptor { get; set; }

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public ICollection<Curso> Cursos { get; set; }
            = new List<Curso>();
    }
}
