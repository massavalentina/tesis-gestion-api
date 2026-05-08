using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class Docente
    {
        [Key]
        public Guid IdDocente { get; set; }

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public ICollection<EspacioCurricular> EspaciosCurriculares { get; set; }
            = new List<EspacioCurricular>();
    }
}
