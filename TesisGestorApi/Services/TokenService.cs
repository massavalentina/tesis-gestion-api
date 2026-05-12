using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Auth;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;

        public TokenService(IConfiguration config, ApplicationDbContext context)
        {
            _config = config;
            _context = context;
        }

        public string GenerarAccessToken(Usuario usuario)
        {
            var clave = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Clave"]!));
            var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);

            var roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList();

            var permisos = usuario.UsuarioRoles
                .SelectMany(ur => ur.Rol.RolPermisos)
                .Select(rp => rp.Permiso.Codigo)
                .Distinct()
                .ToList();

            var claims = new List<Claim>
            {
                new("idUsuario",               usuario.IdUsuario.ToString()),
                new("nombre",                  ObtenerNombreCompleto(usuario)),
                new("apellido",                usuario.Apellido),
                new("email",                   usuario.Email),
                new("requiresPasswordChange",  usuario.RequiereCambioContrasena.ToString().ToLower()),
            };

            foreach (var rol in roles)
                claims.Add(new Claim("roles", rol));

            foreach (var permiso in permisos)
                claims.Add(new Claim("permisos", permiso));

            if (roles.Contains("Preceptor") && usuario.Preceptor?.EsDelegado == true)
            {
                claims.Add(new Claim("tipo_preceptor", "delegado"));
                if (!permisos.Contains("CREDENCIALES_QR_RW"))
                    claims.Add(new Claim("permisos", "CREDENCIALES_QR_RW"));
            }

            if (roles.Contains("Admin"))
                claims.Add(new Claim("es_admin", "true"));

            var expiresMinutes = int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "60");
            var token = new JwtSecurityToken(
                issuer:            _config["Jwt:Emisor"],
                audience:          _config["Jwt:Audiencia"],
                claims:            claims,
                expires:           DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: credenciales
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerarRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public async Task<TokenResponseDto> RefrescarTokenAsync(string refreshToken)
        {
            var tokenEntity = await _context.RefreshTokens
                .Include(rt => rt.Usuario)
                    .ThenInclude(u => u.UsuarioRoles)
                        .ThenInclude(ur => ur.Rol)
                            .ThenInclude(r => r.RolPermisos)
                                .ThenInclude(rp => rp.Permiso)
                .Include(rt => rt.Usuario)
                    .ThenInclude(u => u.Docente)
                .Include(rt => rt.Usuario)
                    .ThenInclude(u => u.Preceptor)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (tokenEntity == null || tokenEntity.Revocado || tokenEntity.Expiracion <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token inválido o vencido.");

            var usuario = tokenEntity.Usuario;
            var nuevoAccessToken = GenerarAccessToken(usuario);
            var nuevoRefreshToken = GenerarRefreshToken();

            var refreshDays = int.Parse(_config["Jwt:RefreshExpiresInDays"] ?? "7");
            tokenEntity.Revocado = true;
            _context.RefreshTokens.Add(new RefreshToken
            {
                Id            = Guid.NewGuid(),
                Token         = nuevoRefreshToken,
                FechaCreacion = DateTime.UtcNow,
                Expiracion    = DateTime.UtcNow.AddDays(refreshDays),
                IdUsuario     = usuario.IdUsuario
            });
            await _context.SaveChangesAsync();

            return new TokenResponseDto(nuevoAccessToken, nuevoRefreshToken);
        }

        private static string ObtenerNombreCompleto(Usuario usuario)
        {
            return $"{usuario.Nombre} {usuario.Apellido}";
        }
    }
}
