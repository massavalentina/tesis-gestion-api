namespace TesisGestorApi.DTOs.ParteDiario
{
    public class TurnoParteDto
    {
        public bool Disponible { get; set; }
        public int Presentes { get; set; }
        public int Ausentes { get; set; }
        public int Retirados { get; set; }
        public int SinRegistro { get; set; }
        public int TotalEstudiantes { get; set; }
        public double PorcentajeAsistencia { get; set; }
        public List<EstudianteParteDto> Estudiantes { get; set; } = new();
        public List<HorarioClaseDto> HorarioClases { get; set; } = new();
    }
}
