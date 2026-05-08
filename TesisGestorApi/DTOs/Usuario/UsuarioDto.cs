namespace TesisGestorApi.DTOs.Usuario
{
    public class UsuarioDto
    {
        public Guid IdUsuario { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string? Telefono { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public List<string> Roles { get; set; } = new();

        // Indica si tiene perfil vinculado (útil para el frontend)
        public Guid? IdDocente { get; set; }
        public Guid? IdPreceptor { get; set; }
    }
}
