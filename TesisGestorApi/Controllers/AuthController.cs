using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TesisGestorApi.DTOs.Auth;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ITokenService tokenService, ILogger<AuthController> logger)
        {
            _authService  = authService;
            _tokenService = tokenService;
            _logger       = logger;
        }

        // POST /api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto)
        {
            try
            {
                var resultado = await _authService.LoginAsync(dto);
                return Ok(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en login.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/auth/refresh
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            try
            {
                var resultado = await _tokenService.RefrescarTokenAsync(dto.RefreshToken);
                return Ok(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al refrescar token.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/auth/solicitar-reset  (olvidé contraseña o provisoria)
        [HttpPost("solicitar-reset")]
        [AllowAnonymous]
        public async Task<IActionResult> SolicitarReset([FromBody] SolicitarResetDto dto)
        {
            try
            {
                await _authService.SolicitarResetAsync(dto);
                return Ok(new { message = "Si los datos son correctos, recibirás un correo con el link de restablecimiento." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al solicitar reset de contraseña.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }

        // POST /api/auth/restablecer-contrasena  (usar link del email)
        [HttpPost("restablecer-contrasena")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDto>> RestablecerContrasena([FromBody] RestablecerContrasenaDto dto)
        {
            try
            {
                var resultado = await _authService.RestablecerContrasenaAsync(dto);
                return Ok(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer contraseña.");
                return StatusCode(500, new { error = "Error interno." });
            }
        }
    }
}
