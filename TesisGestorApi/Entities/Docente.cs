using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Docente
    {
        [Key]
        public Guid IdDocente { get; set; }

        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Documento { get; set; } = null!;

        
        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; }

        
        public ICollection<Curricula> Curriculas { get; set; }
            = new List<Curricula>();
    }

}
