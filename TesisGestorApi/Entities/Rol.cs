using System.ComponentModel.DataAnnotations;

namespace RepoDB.Entities
{
    public class Rol
    {
        [Key]
        public Guid IdRol { get; set; }

        public string Nombre { get; set; } = null!; // Puede ser Docente, Preceptor, Director, Secretario


        public ICollection<UsuarioRol> UsuarioRoles { get; set; } //Puede tener varios
            = new List<UsuarioRol>();
    }

}
