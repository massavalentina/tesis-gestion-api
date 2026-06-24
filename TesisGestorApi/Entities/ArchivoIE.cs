using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class ArchivoIE
    {
        [Key]
        public Guid IdArchivoIE { get; set; }

        public Guid IdIE { get; set; }
        public InstanciaEvaluativa InstanciaEvaluativa { get; set; } = null!;

        public TipoCalificacion TipoCalificacion { get; set; }
        public TipoIE TipoIE { get; set; }

        [Required]
        [MaxLength(300)]
        public string Titulo { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string NombreArchivo { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string UrlArchivo { get; set; } = null!;

        public DateTime FechaEjecucion { get; set; }
        public DateTime FechaCarga { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }

        public Guid IdUsuarioCarga { get; set; }
        public Usuario UsuarioCarga { get; set; } = null!;

        public bool Habilitada { get; set; } = true;

        public Guid? IdArchivoIEAnterior { get; set; }
        public ArchivoIE? ArchivoAnterior { get; set; }

        public ICollection<ArchivoIE> VersionesSiguientes { get; set; } = new List<ArchivoIE>();
        public ICollection<Calificacion> Calificaciones { get; set; } = new List<Calificacion>();
        public ICollection<ArchivoIEBloquePrograma> BloquesPrograma { get; set; } = new List<ArchivoIEBloquePrograma>();
    }
}
