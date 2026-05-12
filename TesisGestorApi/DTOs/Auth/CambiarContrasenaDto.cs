using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.Auth
{
    public class CambiarContrasenaDto
    {
        [Required] public string ContrasenaActual { get; set; } = null!;
        [Required] public string ContrasenaNueva { get; set; } = null!;
        [Required] public string ConfirmacionContrasenaNueva { get; set; } = null!;
    }
}
