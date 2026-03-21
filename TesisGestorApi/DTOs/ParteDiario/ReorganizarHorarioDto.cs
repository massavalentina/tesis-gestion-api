namespace TesisGestorApi.DTOs.ParteDiario
{
    public class ReorganizarHorarioDto
    {
        public Guid CursoId { get; set; }
        public DateOnly Fecha { get; set; }
        /// <summary>IDs de ECs en el orden deseado por el preceptor.</summary>
        public List<Guid> IdECsOrdenados { get; set; } = new();
    }
}
