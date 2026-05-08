using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Usuario
{
    public class CrearUsuarioDto
    {
        [Required]
        public string Nombre { get; set; } = null!;

        [Required]
        public string Apellido { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Documento { get; set; } = null!;

        // Solo dígitos, opcional
        [RegularExpression(@"^\d+$", ErrorMessage = "El teléfono debe contener solo números.")]
        public string? Telefono { get; set; }

        // Lista de roles a asignar: "Admin", "Docente", "Preceptor", "Equipo Directivo", "Secretario"
        // Opcional — un usuario puede crearse sin rol asignado.
        public List<string> Roles { get; set; } = new();
    }
}
