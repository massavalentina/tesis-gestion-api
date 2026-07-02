namespace TesisGestorApi.DTOs.Calificaciones
{
    public class AuditoriaCalificacionesResponseDto
    {
        public List<AuditoriaCalificacionSesionDto> Items { get; set; } = new();
        public int TotalSesiones { get; set; }
        public bool HasMore { get; set; }
    }

    public class AuditoriaCalificacionSesionDto
    {
        public Guid IdSesionAuditoria { get; set; }
        public DateTime Timestamp { get; set; }
        public string Docente { get; set; } = null!;
        public string Origen { get; set; } = null!;
        public int CantidadCambios { get; set; }
        public string? RutaArchivoImportacion { get; set; }
        public List<AuditoriaCalificacionDetalleDto> Cambios { get; set; } = new();
    }

    public class AuditoriaCalificacionDetalleDto
    {
        public Guid IdDetalleAuditoria { get; set; }
        public Guid IdIE { get; set; }
        public Guid IdEstudiante { get; set; }
        public string Estudiante { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string Evaluacion { get; set; } = null!;
        public string TipoCalificacion { get; set; } = null!;
        public int? ValorAnterior { get; set; }
        public int? ValorNuevo { get; set; }
    }
}
