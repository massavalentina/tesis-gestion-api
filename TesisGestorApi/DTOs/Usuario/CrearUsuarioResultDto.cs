namespace TesisGestorApi.DTOs.Usuario
{
    public class CrearUsuarioResultDto
    {
        public UsuarioDto Usuario { get; set; } = null!;
        public string ContrasenaProvisoria { get; set; } = null!;
    }
}
