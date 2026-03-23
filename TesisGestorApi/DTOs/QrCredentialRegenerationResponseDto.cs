namespace TesisGestorApi.DTOs
{
    public class QrCredentialRegenerationResponseDto
    {
        public Guid IdEstudiante { get; set; }
        public Guid IdQr { get; set; }
        public Guid CodigoQr { get; set; }
        public int CredencialesDesactivadas { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
