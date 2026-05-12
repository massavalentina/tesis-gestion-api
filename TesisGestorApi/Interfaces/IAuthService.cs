using TesisGestorApi.DTOs.Auth;

namespace TesisGestorApi.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);
        Task SolicitarResetAsync(SolicitarResetDto dto);
        Task<LoginResponseDto> RestablecerContrasenaAsync(RestablecerContrasenaDto dto);
    }
}
