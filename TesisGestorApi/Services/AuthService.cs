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
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public AuthService(ApplicationDbContext context, IConfiguration config, IEmailSender emailSender)
        {
            _context     = context;
            _config      = config;
            _emailSender = emailSender;
        }

        // ─────────────────────────────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────────────────────────────
        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto)
        {
            var identificador = dto.Identificador.Trim();

            var usuario = await _context.Usuarios
                .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u =>
                    u.Email == identificador.ToLowerInvariant() ||
                    u.Documento == identificador);

            if (usuario == null)
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            // Bloqueo por intentos fallidos
            if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta > DateTime.UtcNow)
            {
                var minutos = (int)Math.Ceiling((usuario.BloqueadoHasta.Value - DateTime.UtcNow).TotalMinutes);
                throw new UnauthorizedAccessException($"Cuenta bloqueada. Intente en {minutos} minuto(s).");
            }

            // Verificar contraseña
            if (!BCrypt.Net.BCrypt.Verify(dto.Contrasena, usuario.Contraseña))
            {
                usuario.IntentosFailidos++;
                if (usuario.IntentosFailidos >= 5)
                    usuario.BloqueadoHasta = DateTime.UtcNow.AddMinutes(15);
                await _context.SaveChangesAsync();
                throw new UnauthorizedAccessException("Credenciales inválidas.");
            }

            // Cuenta activa
            if (!usuario.Activo)
                throw new UnauthorizedAccessException("Cuenta desactivada. Contacte al administrador.");

            // Contraseña vencida
            if (usuario.RequiereCambioContrasena &&
                usuario.FechaVencimientoContrasena.HasValue &&
                usuario.FechaVencimientoContrasena <= DateTime.UtcNow)
            {
                // Si hay un reset por link pendiente, no deactivar — solo bloquear login
                var hayResetPendiente = await _context.PasswordResetTokens
                    .AnyAsync(t => t.IdUsuario == usuario.IdUsuario && !t.Usado && t.Expiracion > DateTime.UtcNow);

                if (hayResetPendiente)
                    throw new UnauthorizedAccessException("Contraseña vencida. Revise su correo para restablecerla.");

                // Contraseña provisoria vencida sin reset → desactivar cuenta
                usuario.Activo = false;
                await _context.SaveChangesAsync();
                throw new UnauthorizedAccessException("Contraseña provisoria vencida. Contacte al administrador.");
            }

            // Reset de intentos y actualización de último login
            usuario.IntentosFailidos = 0;
            usuario.BloqueadoHasta   = null;
            usuario.UltimoLogin      = DateTime.UtcNow;

            var roles        = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList();
            var (jwt, expira) = GenerarJwt(usuario, roles);
            var refreshToken  = await GenerarRefreshTokenAsync(usuario.IdUsuario);

            await _context.SaveChangesAsync();

            return new LoginResponseDto
            {
                AccessToken              = jwt,
                RefreshToken             = refreshToken,
                AccessTokenExpira        = expira,
                RequiereCambioContrasena = usuario.RequiereCambioContrasena,
                IdUsuario                = usuario.IdUsuario,
                Nombre                   = usuario.Nombre,
                Apellido                 = usuario.Apellido,
                Email                    = usuario.Email,
                Documento                = usuario.Documento,
                Roles                    = roles,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOLICITAR RESET (olvide mi contraseña / cambiar clave desde perfil)
        // ─────────────────────────────────────────────────────────────────────
        public async Task SolicitarResetAsync(SolicitarResetDto dto)
        {
            // Buscar por email + documento (mapeamos por DNI para distinguir usuarios con mismo email)
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u =>
                    u.Email == dto.Email.Trim().ToLowerInvariant() &&
                    u.Documento == dto.Documento.Trim());

            // Respuesta idéntica si el usuario no existe (evita enumeración)
            if (usuario == null || !usuario.Activo) return;

            // Invalidar tokens de reset anteriores
            await _context.PasswordResetTokens
                .Where(t => t.IdUsuario == usuario.IdUsuario && !t.Usado)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Usado, true));

            // Generar nuevo token (1 hora de validez)
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            var token = Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").Replace("=", ""); // URL-safe

            var resetToken = new PasswordResetToken
            {
                Id            = Guid.NewGuid(),
                Token         = token,
                IdUsuario     = usuario.IdUsuario,
                FechaCreacion = DateTime.UtcNow,
                Expiracion    = DateTime.UtcNow.AddMinutes(15),
                Usado         = false,
            };
            _context.PasswordResetTokens.Add(resetToken);

            // Vencer contraseña actual inmediatamente
            usuario.RequiereCambioContrasena   = true;
            usuario.FechaVencimientoContrasena = DateTime.UtcNow.AddSeconds(-1);

            await _context.SaveChangesAsync();

            // Enviar email con link
            var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:4200";
            var link = $"{frontendUrl}/restablecer-contrasena?token={Uri.EscapeDataString(token)}&dni={Uri.EscapeDataString(usuario.Documento)}";

            await _emailSender.SendAsync(
                to: usuario.Email,
                subject: "Restablecimiento de contraseña",
                htmlBody: $@"
                    <p>Hola <strong>{usuario.Nombre} {usuario.Apellido}</strong>,</p>
                    <p>Recibimos una solicitud para restablecer la contraseña de la cuenta asociada al DNI <strong>{usuario.Documento}</strong>.</p>
                    <p>Hacé clic en el siguiente botón para crear una nueva contraseña. El link es válido por <strong>15 minutos</strong>.</p>
                    <p style='margin:24px 0;'>
                      <a href='{link}' style='background:#0284c7;color:white;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold;'>
                        Restablecer contraseña
                      </a>
                    </p>
                    <p>Si no solicitaste este cambio, ignorá este correo. Tu contraseña anterior ya no es válida para ingresar al sistema.</p>
                    <p style='color:#64748b;font-size:12px;'>Link directo: {link}</p>"
            );
        }

        // ─────────────────────────────────────────────────────────────────────
        // RESTABLECER CONTRASEÑA (con token del email)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<LoginResponseDto> RestablecerContrasenaAsync(RestablecerContrasenaDto dto)
        {
            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.Usuario)
                    .ThenInclude(u => u.UsuarioRoles)
                        .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(t => t.Token == dto.Token && !t.Usado);

            if (resetToken == null || resetToken.Expiracion <= DateTime.UtcNow)
                throw new InvalidOperationException("El link de restablecimiento es inválido o ya expiró.");

            // Verificar que el DNI coincida con el usuario del token (seguridad extra)
            if (!string.Equals(resetToken.Usuario.Documento, dto.Documento.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El link de restablecimiento es inválido o ya expiró.");

            var usuario = resetToken.Usuario;

            ValidarNuevaContrasena(dto.ContrasenaNueva, dto.ConfirmacionContrasenaNueva, usuario.Contraseña);

            usuario.Contraseña                 = BCrypt.Net.BCrypt.HashPassword(dto.ContrasenaNueva, workFactor: 12);
            usuario.RequiereCambioContrasena   = false;
            usuario.FechaVencimientoContrasena = null;
            resetToken.Usado                   = true;

            var roles        = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList();
            var (jwt, expira) = GenerarJwt(usuario, roles);
            var refreshToken  = await GenerarRefreshTokenAsync(usuario.IdUsuario);

            await _context.SaveChangesAsync();

            return new LoginResponseDto
            {
                AccessToken              = jwt,
                RefreshToken             = refreshToken,
                AccessTokenExpira        = expira,
                RequiereCambioContrasena = false,
                IdUsuario                = usuario.IdUsuario,
                Nombre                   = usuario.Nombre,
                Apellido                 = usuario.Apellido,
                Email                    = usuario.Email,
                Documento                = usuario.Documento,
                Roles                    = roles,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS PRIVADOS
        // ─────────────────────────────────────────────────────────────────────
        private static void ValidarNuevaContrasena(string nueva, string confirmacion, string hashActual)
        {
            if (nueva.Length < 6)
                throw new ArgumentException("La nueva contraseña debe tener al menos 6 caracteres.");
            if (!nueva.Any(char.IsUpper))
                throw new ArgumentException("La nueva contraseña debe contener al menos una mayúscula.");
            if (!nueva.Any(char.IsDigit))
                throw new ArgumentException("La nueva contraseña debe contener al menos un número.");
            if (nueva != confirmacion)
                throw new ArgumentException("La confirmación no coincide con la nueva contraseña.");
            if (BCrypt.Net.BCrypt.Verify(nueva, hashActual))
                throw new ArgumentException("La nueva contraseña no puede ser igual a la actual.");
        }

        private (string token, DateTime expira) GenerarJwt(Usuario usuario, List<string> roles)
        {
            var jwtSection  = _config.GetSection("Jwt");
            var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expira      = DateTime.UtcNow.AddMinutes(int.Parse(jwtSection["ExpiresInMinutes"]!));

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,   usuario.IdUsuario.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim("nombre",                      usuario.Nombre),
                new Claim("apellido",                    usuario.Apellido),
                new Claim("requiresPasswordChange",      usuario.RequiereCambioContrasena.ToString().ToLower()),
            };

            foreach (var rol in roles)
                claims.Add(new Claim(ClaimTypes.Role, rol));

            var token = new JwtSecurityToken(
                issuer:             jwtSection["Issuer"],
                audience:           jwtSection["Audience"],
                claims:             claims,
                expires:            expira,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expira);
        }

        private async Task<string> GenerarRefreshTokenAsync(Guid idUsuario)
        {
            await _context.RefreshTokens
                .Where(rt => rt.IdUsuario == idUsuario && !rt.Revocado)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.Revocado, true));

            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            var token = Convert.ToBase64String(bytes);

            _context.RefreshTokens.Add(new RefreshToken
            {
                Id            = Guid.NewGuid(),
                Token         = token,
                FechaCreacion = DateTime.UtcNow,
                Expiracion    = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshExpiresInDays"]!)),
                Revocado      = false,
                IdUsuario     = idUsuario,
            });

            return token;
        }
    }
}
