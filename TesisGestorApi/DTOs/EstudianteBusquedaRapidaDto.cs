namespace TesisGestorApi.DTOs
{
    public class EstudianteBusquedaRapidaDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Documento { get; set; } = null!;
        public string Curso { get; set; } = "-";      // por ahora fijo si no tenés el join armado
        public bool RegistradoHoy { get; set; }
    }

}
