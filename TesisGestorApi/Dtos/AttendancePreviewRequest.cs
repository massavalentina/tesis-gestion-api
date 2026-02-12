using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Dtos
{
    public class AttendancePreviewRequest
    {
        [Required]
        public string Qr { get; set; } = null!;

        [Required]
        public string Turno { get; set; } = null!;
    }
}

