namespace TesisGestorApi.DTOs
{
    public class QrEmailProgressDto
    {
        public Guid JobId { get; set; }
        public string Estado { get; set; } = "RUNNING"; // RUNNING | COMPLETED | FAILED

        public int Total { get; set; }
        public int Procesados { get; set; }
        public int Enviados { get; set; }
        public int Omitidos { get; set; }
        public int Errores { get; set; }

        public string? UltimoDestino { get; set; } // mail al que se intentó enviar
        public string? UltimoMensaje { get; set; }

        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }
    }
}