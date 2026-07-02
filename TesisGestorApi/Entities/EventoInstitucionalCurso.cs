namespace TesisGestorApi.Entities
{
    public class EventoInstitucionalCurso
    {
        public Guid IdEvento { get; set; }
        public EventoInstitucional EventoInstitucional { get; set; } = null!;

        public Guid IdCurso { get; set; }
        public Curso Curso { get; set; } = null!;
    }
}
