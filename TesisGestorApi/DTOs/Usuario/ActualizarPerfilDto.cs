using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Usuario
{
    public class ActualizarPerfilDto
    {
        [Required]
        public string Nombre { get; set; } = null!;

        [Required]
        public string Apellido { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public string? Telefono { get; set; }
    }
}
