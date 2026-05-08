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

            var nombre = ObtenerNombreCompleto(usuario);

            var claims = new List<Claim>
            {
                new("idUsuario", usuario.IdUsuario.ToString()),
                new("nombre", nombre),
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

            if (roles.Contains("Administrador"))
                claims.Add(new Claim("es_admin", "true"));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Emisor"],
                audience: _config["Jwt:Audiencia"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
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
            var usuario = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                        .ThenInclude(r => r.RolPermisos)
                            .ThenInclude(rp => rp.Permiso)
                .Include(u => u.Docente)
                .Include(u => u.Preceptor)
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

            if (usuario == null || usuario.RefreshTokenVencimiento <= DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token inválido o vencido.");

            var nuevoAccessToken = GenerarAccessToken(usuario);
            var nuevoRefreshToken = GenerarRefreshToken();

            usuario.RefreshToken = nuevoRefreshToken;
            usuario.RefreshTokenVencimiento = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return new TokenResponseDto(nuevoAccessToken, nuevoRefreshToken);
        }

        private static string ObtenerNombreCompleto(Usuario usuario)
        {
            if (usuario.Docente != null)
                return $"{usuario.Docente.Nombre} {usuario.Docente.Apellido}";
            if (usuario.Preceptor != null)
                return $"{usuario.Preceptor.Nombre} {usuario.Preceptor.Apellido}";
            return usuario.Mail;
        }
    }
}
