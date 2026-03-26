namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliveryStudentRowDto
    {
        public Guid IdEstudiante { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;

        public string? TutorPrincipalNombre { get; set; }
        public string? TutorPrincipalEmail { get; set; }

        public string Estado { get; set; } = "SIN_QR";
        public DateTime? FechaGeneracionQr { get; set; }
    }
}
