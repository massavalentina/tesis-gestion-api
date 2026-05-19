namespace TesisGestorApi.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        string NombreCompleto { get; }
    }
}
