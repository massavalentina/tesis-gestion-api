using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Auth
{
    public class SolicitarResetDto
    {
        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string Documento { get; set; } = null!;
    }
}
