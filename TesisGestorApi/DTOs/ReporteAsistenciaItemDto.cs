namespace TesisGestorApi.DTOs
{
    public class ReporteAsistenciaItemDto
    {
        public Guid IdEstudiante { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public int Presencias { get; set; }
        public decimal Inasistencias { get; set; }
        public int LlegadasTarde { get; set; }
        public int AusentePorLLT { get; set; }
        public int RetirosAnticipados { get; set; }
        public decimal PorcentajeAsistencia { get; set; }
        public bool TeaGeneral { get; set; }
    }
}
