namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliverySingleRequestDto
    {
        public Guid IdCurso { get; set; }
        public string? Asunto { get; set; }
        public string? MensajePersonalizado { get; set; }
    }
}
