using TesisGestorApi.DTOs.Usuarios;

namespace TesisGestorApi.Interfaces
{
    public interface IUsuariosRolesService
    {
        Task<List<UsuarioConRolesDto>> GetUsuariosConRolesAsync(CancellationToken ct = default);
        Task<List<RolDto>> GetRolesDisponiblesAsync(CancellationToken ct = default);
        Task AsignarRolAsync(Guid idUsuario, Guid idRol, CancellationToken ct = default);
        Task QuitarRolAsync(Guid idUsuario, Guid idRol, CancellationToken ct = default);
        Task ActualizarDelegadoAsync(Guid idUsuario, bool esDelegado, CancellationToken ct = default);
    }
}
