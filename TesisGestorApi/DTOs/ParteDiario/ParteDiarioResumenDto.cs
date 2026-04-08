namespace TesisGestorApi.DTOs.ParteDiario
{
    public class ParteDiarioResumenDto
    {
        public TurnoParteDto Manana { get; set; } = new();
        public TurnoParteDto Tarde { get; set; } = new();
    }
}
