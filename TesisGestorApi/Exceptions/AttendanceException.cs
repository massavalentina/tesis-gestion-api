
namespace TesisGestorApi.Exceptions;

public class AttendanceException : Exception
{
    public string Code { get; }

    public AttendanceException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
