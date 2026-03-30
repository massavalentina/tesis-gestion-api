namespace TesisGestorApi.DTOs.ParteDiario
{
    public class IntercambiarHorarioDto
    {
        public Guid IdHorario1 { get; set; }
        public Guid IdHorario2 { get; set; }
        public Guid CursoId    { get; set; }
        public DateOnly Fecha  { get; set; }
    }
}
