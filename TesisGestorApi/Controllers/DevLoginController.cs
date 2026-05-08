// ⚠️ SOLO DESARROLLO — Eliminar este controlador antes de pasar a producción
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Auth;
using TesisGestorApi.Interfaces;

[ApiController]
[Route("api/dev")]
public class DevLoginController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;

    public DevLoginController(ApplicationDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    // ⚠️ SOLO DESARROLLO — Eliminar antes de producción
    [HttpGet("login/usuarios")]
    public async Task<ActionResult<List<DevLoginUsuarioDto>>> GetUsuarios()
    {
        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
            .Select(u => new DevLoginUsuarioDto(
                u.IdUsuario,
                u.Mail,
                u.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
            ))
            .ToListAsync();

        return Ok(usuarios);
    }

    // ⚠️ SOLO DESARROLLO — Eliminar antes de producción
    [HttpGet("login/{idUsuario}")]
    public async Task<ActionResult<TokenResponseDto>> LoginDev(Guid idUsuario)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
                    .ThenInclude(r => r.RolPermisos)
                        .ThenInclude(rp => rp.Permiso)
            .Include(u => u.Docente)
            .Include(u => u.Preceptor)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);

        if (usuario == null)
            return NotFound();

        var accessToken = _tokenService.GenerarAccessToken(usuario);
        var refreshToken = _tokenService.GenerarRefreshToken();

        usuario.RefreshToken = refreshToken;
        usuario.RefreshTokenVencimiento = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync();

        return Ok(new TokenResponseDto(accessToken, refreshToken));
    }
}
