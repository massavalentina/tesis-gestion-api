namespace TesisGestorApi.Dtos;

public class AttendanceConfirmRequest
{
    public string Turno { get; set; } = null!;
    public Guid AttendanceTypeId { get; set; }
    public List<Guid> StudentIds { get; set; } = new();
}
