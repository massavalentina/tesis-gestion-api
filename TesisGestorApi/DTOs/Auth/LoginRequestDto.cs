namespace TesisGestorApi.DTOs.Auth
{
    public class LoginRequestDto
    {
        public string Identificador { get; set; } = null!;  // email O DNI
        public string Contrasena { get; set; } = null!;
    }
}
