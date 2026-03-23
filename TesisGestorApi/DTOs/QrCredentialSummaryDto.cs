namespace TesisGestorApi.DTOs
{
    public class QrCredentialSummaryDto
    {
        public Guid? IdCurso { get; set; }
        public string? CursoCodigo { get; set; }
        public int TotalAlumnosActivos { get; set; }
        public int TotalQrActivos { get; set; }
        public int TotalPendientesGenerar { get; set; }
    }
}
