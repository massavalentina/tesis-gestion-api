// ⚠️ SOLO DESARROLLO — Eliminar este controlador antes de pasar a producción
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Auth;
using TesisGestorApi.Entities;
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
        // Materializar primero para evitar problemas de traducción EF con colecciones anidadas
        var entidades = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
            .OrderBy(u => u.Apellido)
            .ToListAsync();

        var usuarios = entidades.Select(u => new DevLoginUsuarioDto(
            u.IdUsuario,
            u.Email,
            u.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList()
        )).ToList();

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

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            FechaCreacion = DateTime.UtcNow,
            Expiracion = DateTime.UtcNow.AddDays(7),
            IdUsuario = usuario.IdUsuario
        });
        await _context.SaveChangesAsync();

        return Ok(new TokenResponseDto(accessToken, refreshToken));
    }
}
