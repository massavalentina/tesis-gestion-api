namespace TesisGestorApi.DTOs
{
    public class QrCredentialStudentStatusDto
    {
        public Guid IdEstudiante { get; set; }
        public string Estado { get; set; } = "NO_GENERADO";
        public int VersionQr { get; set; }
        public DateTime? FechaGeneracion { get; set; }
    }
}
