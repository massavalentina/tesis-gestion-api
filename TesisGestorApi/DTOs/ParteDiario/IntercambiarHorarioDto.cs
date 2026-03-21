namespace TesisGestorApi.DTOs.ParteDiario
{
    public class IntercambiarHorarioDto
    {
        public Guid IdEC1    { get; set; }
        public Guid IdEC2    { get; set; }
        public Guid CursoId  { get; set; }
        public DateOnly Fecha { get; set; }
    }
}
