using RepoDB.Entities;

namespace TesisGestorApi.DTOs
{
    public class AsistenciaGetDTO
    {
        public Guid Id { get; set; }
        public DateOnly Fecha { get; set; }
        public decimal ValorTotal { get; set; }
        public string NombreCompleto { get; set; }
        public string Documento { get; set; }
        public string CodigoManana { get; set; }
        public string CodigoTarde { get; set; }

    }
}
