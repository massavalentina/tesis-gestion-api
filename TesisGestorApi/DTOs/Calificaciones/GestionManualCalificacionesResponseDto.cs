namespace TesisGestorApi.DTOs.Calificaciones
{
    public class GestionManualEstudianteDto
    {
        public Guid IdEstudiante { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
    }

    public class CalificacionVigenteDto
    {
        public Guid IdCalificacion { get; set; }
        public Guid IdIE { get; set; }
        public Guid IdEstudiante { get; set; }
        public string TipoCalificacion { get; set; } = null!;
        public int? Puntaje { get; set; }
        public DateTime FechaCarga { get; set; }
        public string Origen { get; set; } = null!;
    }
}
