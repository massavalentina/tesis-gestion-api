namespace TesisGestorApi.DTOs.Calificaciones
{
    public class InstanciaEvaluativaResumenDto
    {
        public Guid IdIE { get; set; }
        public Guid IdEC { get; set; }
        public int Nro { get; set; }
        public string Estado { get; set; } = null!;
        public InstanciaEvaluativaArchivosDto Archivos { get; set; } = new();
    }

    public class InstanciaEvaluativaArchivosDto
    {
        public ArchivoIEResumenDto? NotaOriginal { get; set; }
        public ArchivoIEResumenDto? Recuperatorio1 { get; set; }
        public ArchivoIEResumenDto? Recuperatorio2 { get; set; }
    }

    public class ArchivoIEResumenDto
    {
        public Guid IdArchivoIE { get; set; }
        public string TipoCalificacion { get; set; } = null!;
        public string TipoIE { get; set; } = null!;
        public string Titulo { get; set; } = null!;
        public DateTime FechaEjecucion { get; set; }
        public DateTime FechaCarga { get; set; }
        public string NombreArchivo { get; set; } = null!;
    }
}
