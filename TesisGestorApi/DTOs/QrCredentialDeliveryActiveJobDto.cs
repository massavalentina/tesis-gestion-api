namespace TesisGestorApi.DTOs
{
    public class QrCredentialDeliveryActiveJobDto
    {
        public Guid JobId { get; set; }
        public Guid IdCurso { get; set; }
        public string CursoCodigo { get; set; } = string.Empty;
        public string Alcance { get; set; } = "PENDIENTES";
        public string Estado { get; set; } = "RUNNING";

        public int Total { get; set; }
        public int Procesados { get; set; }
        public int Enviados { get; set; }
        public int Omitidos { get; set; }
        public int Errores { get; set; }

        public string? UltimoMensaje { get; set; }
        public DateTime Inicio { get; set; }
    }
}
