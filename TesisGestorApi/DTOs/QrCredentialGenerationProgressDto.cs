namespace TesisGestorApi.DTOs
{
    public class QrCredentialGenerationProgressDto
    {
        public Guid JobId { get; set; }
        public string Estado { get; set; } = "RUNNING";
        public int Total { get; set; }
        public int Procesados { get; set; }
        public int Generados { get; set; }
        public int Desactivados { get; set; }
        public int Omitidos { get; set; }
        public int Errores { get; set; }
        public string? UltimoEstudiante { get; set; }
        public string? UltimoMensaje { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime? Fin { get; set; }
    }
}
