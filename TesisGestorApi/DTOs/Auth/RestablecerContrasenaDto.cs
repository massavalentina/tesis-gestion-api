using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Auth
{
    public class RestablecerContrasenaDto
    {
        [Required]
        public string Token { get; set; } = null!;

        [Required]
        public string Documento { get; set; } = null!;

        [Required]
        public string ContrasenaNueva { get; set; } = null!;

        [Required]
        public string ConfirmacionContrasenaNueva { get; set; } = null!;
    }
}
