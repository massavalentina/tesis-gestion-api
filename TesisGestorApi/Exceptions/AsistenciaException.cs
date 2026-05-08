
namespace TesisGestorApi.Exceptions;

public class AsistenciaException : Exception
{
    public string Code { get; }
    public object? Details { get; }

    public AsistenciaException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public AsistenciaException(string code, string message, object? details)
        : base(message)
    {
        Code = code;
        Details = details;
    }
}
