namespace TesisGestorApi.DTOs
{
    public class DetalleAsistenciaEstudianteDto
    {
        public DateOnly Fecha { get; set; }
        public string CodigoManana { get; set; } = null!;
        public string CodigoTarde { get; set; } = null!;
        public decimal ValorTotal { get; set; }
        public string? HoraEntradaManana { get; set; }
        public string? HoraSalidaManana { get; set; }
    }
}
