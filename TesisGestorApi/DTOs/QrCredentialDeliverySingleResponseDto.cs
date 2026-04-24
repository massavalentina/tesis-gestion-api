namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliverySingleResponseDto
    {
        public Guid IdEstudiante { get; set; }
        public string Estado { get; set; } = "ENVIADO";
        public string? Destino { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }
}
