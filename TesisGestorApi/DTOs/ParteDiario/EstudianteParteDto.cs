namespace TesisGestorApi.DTOs.ParteDiario
{
    public class EstudianteParteDto
    {
        public Guid IdEstudiante { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;

        /// <summary>Presente | Ausente | Retirado | SinRegistro</summary>
        public string Estado { get; set; } = null!;
        public string? CodigoAsistencia { get; set; }
        public string? HoraEntrada { get; set; }
        public string? HoraSalida { get; set; }
    }
}
