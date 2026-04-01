namespace TesisGestorApi.DTOs
{
    public class ReporteDocenteItemDto
    {
        public Guid IdEstudiante { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public int Presencias { get; set; }
        public int Inasistencias { get; set; }
        public int LlegadasTarde { get; set; }
        public int RetirosAnticipados { get; set; }
        public decimal PorcentajeAsistencia { get; set; }
        public bool TeaGeneral { get; set; }
    }
}
