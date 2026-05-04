namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliveryProgressDto
    {
        public Guid JobId { get; set; }
        public string Estado { get; set; } = "RUNNING"; // RUNNING | PAUSING | PAUSED | CANCELLING | CANCELLED | COMPLETED | FAILED

        public int Total { get; set; }
        public int Procesados { get; set; }
        public int Enviados { get; set; }
        public int Omitidos { get; set; }
        public int Errores { get; set; }

        public string? UltimoDestino { get; set; }
        public string? UltimoEstudiante { get; set; }
        public string? UltimoMensaje { get; set; }
        public List<string> DetallesErrores { get; set; } = new();

        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }
    }
}
