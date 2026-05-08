using TesisGestorApi.DTOs.Auth;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Interfaces
{
    public interface ITokenService
    {
        string GenerarAccessToken(Usuario usuario);
        string GenerarRefreshToken();
        Task<TokenResponseDto> RefrescarTokenAsync(string refreshToken);
    }
}
