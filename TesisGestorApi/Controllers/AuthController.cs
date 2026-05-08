using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Auth;
using TesisGestorApi.Interfaces;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;

    public AuthController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        try
        {
            var resultado = await _tokenService.RefrescarTokenAsync(request.RefreshToken);
            return Ok(resultado);
        }
        catch
        {
            return Unauthorized();
        }
    }
}
