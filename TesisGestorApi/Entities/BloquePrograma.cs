using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities;

public class BloquePrograma
{
    [Key]
    public Guid IdBloquePrograma { get; set; }

    public Guid IdPrograma { get; set; }
    public Programa Programa { get; set; } = null!;

    // Para tipo Unidad: es el ID de la propia unidad.
    // Para tipo Tema:   es el ID de la unidad padre (necesario para el rollup).
    public Guid IdUnidad { get; set; }
    public Unidad Unidad { get; set; } = null!;

    // Solo no-null cuando Tipo == Tema
    public Guid? IdTema { get; set; }
    public Tema? Tema { get; set; }

    public TipoBloquePrograma Tipo { get; set; }
    public EstadoBloque Estado { get; set; } = EstadoBloque.PendienteDar;

    public ICollection<ClaseBloquePrograma> ClasesBloquePrograma { get; set; } = new List<ClaseBloquePrograma>();
    public ICollection<ArchivoIEBloquePrograma> ArchivosIE { get; set; } = new List<ArchivoIEBloquePrograma>();
}
