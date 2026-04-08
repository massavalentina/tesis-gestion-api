namespace TesisGestorApi.DTOs
{
    public class EstudianteBusquedaRapidaDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string Curso { get; set; } = "-";
        public bool RegistradoHoy { get; set; }
        public bool TeaGeneral { get; set; }
    }

}
