namespace TesisGestorApi.Entities;

public class ClaseBloquePrograma
{
    public Guid IdClasePlanificacion { get; set; }
    public Planificacion Planificacion { get; set; } = null!;

    public Guid IdBloquePrograma { get; set; }
    public BloquePrograma BloquePrograma { get; set; } = null!;
}
