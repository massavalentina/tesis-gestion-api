
namespace TesisGestorApi.Dtos;

public class AttendanceScanResponse
{
    public StudentDto Student { get; set; } = null!;
    public AttendanceDto Attendance { get; set; } = null!;
}

public class StudentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Course { get; set; } = null!;
}

public class AttendanceDto
{
    public string Time { get; set; } = null!;
    public string AttendanceType { get; set; } = null!;
    public string Turno { get; set; } = null!;
}
