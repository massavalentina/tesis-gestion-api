namespace TesisGestorApi.Dtos;

public class AttendanceScanRequest
{
    public Guid Qr { get; set; }
    public string Turno { get; set; } = null!;
    public Guid AttendanceTypeId { get; set; }
}
