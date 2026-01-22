namespace RepoDB.Entities
{
    public class UsuarioRol
    {
        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public Guid IdRol { get; set; }
        public Rol Rol { get; set; } = null!;
    }

}
