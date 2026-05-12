using TesisGestorApi.DTOs.Usuario;

namespace TesisGestorApi.Interfaces
{
    public interface IUsuarioService
    {
        Task<CrearUsuarioResultDto> CrearAsync(CrearUsuarioDto dto);
        Task<UsuarioDto> ObtenerPorIdAsync(Guid id);
        Task<List<UsuarioDto>> ObtenerTodosAsync();
        Task DesactivarAsync(Guid id);
        Task ActivarAsync(Guid id);
        Task<bool> EmailExisteAsync(string email);
        Task<bool> DocumentoExisteAsync(string documento);
        Task<UsuarioDto> ActualizarPerfilAsync(Guid id, ActualizarPerfilDto dto);
    }
}
