namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliveryRequestDto
    {
        public Guid IdCurso { get; set; }
        public string Alcance { get; set; } = "PENDIENTES"; // TODOS | PENDIENTES
        public bool ModoEstricto { get; set; } = false;
        public string? Asunto { get; set; }
        public string? MensajePersonalizado { get; set; }
    }
}
