using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities;

public class Planificacion
{
    [Key]
    public Guid IdPlanificacion { get; set; }

    public Guid IdDocente { get; set; }
    public Docente Docente { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Titulo { get; set; } = null!;

    [MaxLength(2000)]
    public string? Descripcion { get; set; }

    public DateOnly? FechaDesde { get; set; }   // FechaEstimada: cuándo el docente planea dar la clase
    public DateOnly? FechaHasta { get; set; }   // FechaDictada: cuándo la clase fue realmente dictada (null = pendiente)

    // PendienteDar o Dado (las otras dimensiones —PendienteEvaluar/Evaluado— son futuras)
    public EstadoBloque Estado { get; set; } = EstadoBloque.PendienteDar;

    [MaxLength(500)]
    public string? Url { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<ClaseBloquePrograma> ClasesBloquePrograma { get; set; } = new List<ClaseBloquePrograma>();
}
