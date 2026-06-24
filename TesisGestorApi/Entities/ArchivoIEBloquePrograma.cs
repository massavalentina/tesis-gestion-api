namespace TesisGestorApi.Entities;

public class ArchivoIEBloquePrograma
{
    public Guid IdArchivoIE { get; set; }
    public ArchivoIE ArchivoIE { get; set; } = null!;

    public Guid IdBloquePrograma { get; set; }
    public BloquePrograma BloquePrograma { get; set; } = null!;
}
