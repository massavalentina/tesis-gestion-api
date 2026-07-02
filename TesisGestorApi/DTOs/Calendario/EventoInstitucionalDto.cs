namespace TesisGestorApi.DTOs.Calendario
{
    public class EventoInstitucionalDto
    {
        public Guid IdEvento { get; set; }
        public string Titulo { get; set; } = null!;
        public string? Descripcion { get; set; }
        public int TipoEvento { get; set; }
        public string TipoEventoLabel { get; set; } = null!;
        public string FechaInicio { get; set; } = null!;
        public string FechaFin { get; set; } = null!;
        public bool ContabilizaAsistencia { get; set; }
        public bool CambioActividad { get; set; }
        public string? ComentarioCambioActividad { get; set; }
        public int AnioLectivo { get; set; }
        public List<CursoEventoDto> Cursos { get; set; } = new();
    }

    public class CursoEventoDto
    {
        public Guid IdCurso { get; set; }
        public string Label { get; set; } = null!;
    }
}
