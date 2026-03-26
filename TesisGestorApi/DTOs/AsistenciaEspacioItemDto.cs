namespace TesisGestorApi.DTOs;
public class AsistenciaEspacioItemDto
{
    public Guid?   IdAsistenciaEspacio { get; set; }
    public Guid    IdEC                { get; set; }
    public Guid?   IdClaseDictada      { get; set; }
    public string  NombreMateria       { get; set; } = null!;
    public string  HorarioEntrada         { get; set; } = null!;
    public string  HorarioSalida          { get; set; } = null!;
    public string? HorarioEntradaOriginal { get; set; }  // null si no fue modificado
    public string? HorarioSalidaOriginal  { get; set; }  // null si no fue modificado
    public bool?   Dictada                { get; set; }
    public bool?   Presente               { get; set; }
}
