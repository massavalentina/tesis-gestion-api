namespace TesisGestorApi.DTOs
{
    public class TipoAsistenciaRapidaDTO
    {
        public Guid Id { get; set; }
        public string Codigo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
    }
}
