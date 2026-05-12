using System.Text.Json.Serialization;

namespace TesisGestorApi.DTOs.Auth
{
    public class LoginResponseDto
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime AccessTokenExpira { get; set; }

        [JsonPropertyName("requiresPasswordChange")]
        public bool RequiereCambioContrasena { get; set; }

        public Guid IdUsuario { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public List<string> Roles { get; set; } = new();
    }
}
