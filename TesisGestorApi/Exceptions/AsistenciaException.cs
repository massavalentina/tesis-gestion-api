
namespace TesisGestorApi.Exceptions;

public class AsistenciaException : Exception
{
    public string Code { get; }

    public AsistenciaException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
