namespace TesisGestorApi.DTOs.Retiro
{
    public class RegistrarReingresoDto
    {
        public Guid IdRetiro { get; set; }
        public TimeSpan HorarioReingreso { get; set; }
        public string NombrePreceptor { get; set; } = null!;
    }
}
