using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public enum TipoOperacionEvento
    {
        Creacion = 1,
        Modificacion = 2,
        Eliminacion = 3
    }

    public class AuditoriaEventoInstitucional
    {
        [Key]
        public Guid IdAuditoria { get; set; }

        public Guid IdEvento { get; set; }
        public EventoInstitucional EventoInstitucional { get; set; } = null!;

        public TipoOperacionEvento TipoOperacion { get; set; }

        [MaxLength(4000)]
        public string? ValoresAnteriores { get; set; }

        [MaxLength(4000)]
        public string? ValoresNuevos { get; set; }

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public DateTime FechaRegistro { get; set; }
    }
}
