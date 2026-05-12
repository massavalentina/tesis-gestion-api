using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Usuario;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        // Los roles que generan un perfil propio en su tabla correspondiente
        private static readonly HashSet<string> RolesConPerfil =
            new(StringComparer.OrdinalIgnoreCase) { "Docente", "Preceptor" };

        public UsuarioService(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ALTA
        // ─────────────────────────────────────────────────────────────────────
        public async Task<CrearUsuarioResultDto> CrearAsync(CrearUsuarioDto dto)
        {
            // 1. Verificar unicidad de email y documento ANTES de hashear.
            bool emailExiste = await _context.Usuarios
                .AnyAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
            if (emailExiste)
                throw new InvalidOperationException($"El email '{dto.Email}' ya está registrado.");

            bool documentoExiste = await _context.Usuarios
                .AnyAsync(u => u.Documento == dto.Documento);
            if (documentoExiste)
                throw new InvalidOperationException($"El documento '{dto.Documento}' ya está registrado.");

            // 2. Verificar que todos los roles existan en la base de datos.
            var roles = new List<Rol>();
            foreach (var rolNombre in dto.Roles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var rol = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Nombre == rolNombre)
                    ?? throw new ArgumentException($"El rol '{rolNombre}' no existe.");
                roles.Add(rol);
            }

            // 3. Generar contraseña aleatoria (12 chars: upper+lower+digit).
            string contrasenaProvisoria = GenerarContrasenaAleatoria();

            // 4. Hashear con BCrypt workFactor 12.
            string contraseñaHash = BCrypt.Net.BCrypt.HashPassword(contrasenaProvisoria, workFactor: 12);

            // 5. Construir la entidad Usuario.
            var usuario = new Usuario
            {
                IdUsuario                  = Guid.NewGuid(),
                Nombre                     = dto.Nombre.Trim(),
                Apellido                   = dto.Apellido.Trim(),
                Email                      = dto.Email.Trim().ToLowerInvariant(),
                Documento                  = dto.Documento.Trim(),
                Telefono                   = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
                Contraseña                 = contraseñaHash,
                Activo                     = true,
                FechaCreacion              = DateTime.UtcNow,
                IntentosFailidos           = 0,
                BloqueadoHasta             = null,
                RequiereCambioContrasena   = true,
                FechaVencimientoContrasena = DateTime.UtcNow.AddDays(7),
            };

            _context.Usuarios.Add(usuario);

            // 6. Crear las relaciones Usuario-Rol.
            foreach (var rol in roles)
            {
                _context.UsuariosRoles.Add(new UsuarioRol
                {
                    IdUsuario = usuario.IdUsuario,
                    IdRol     = rol.IdRol,
                });
            }

            // 7. Si algún rol requiere perfil propio, crearlo.
            if (roles.Any(r => r.Nombre.Equals("Docente", StringComparison.OrdinalIgnoreCase)))
            {
                _context.Docentes.Add(new Docente
                {
                    IdDocente = Guid.NewGuid(),
                    IdUsuario = usuario.IdUsuario,
                });
            }
            if (roles.Any(r => r.Nombre.Equals("Preceptor", StringComparison.OrdinalIgnoreCase)))
            {
                _context.Preceptores.Add(new Preceptor
                {
                    IdPreceptor = Guid.NewGuid(),
                    IdUsuario   = usuario.IdUsuario,
                });
            }

            await _context.SaveChangesAsync();

            // 8. Enviar email con contraseña provisoria.
            try
            {
                await _emailSender.SendAsync(
                    to: usuario.Email,
                    subject: "Credenciales de acceso al Sistema de Gestión Escolar",
                    htmlBody: $@"
                        <p>Hola {usuario.Nombre},</p>
                        <p>Tu cuenta ha sido creada. Tus credenciales de acceso son:</p>
                        <ul>
                          <li><strong>Email:</strong> {usuario.Email}</li>
                          <li><strong>Contraseña provisoria:</strong> {contrasenaProvisoria}</li>
                        </ul>
                        <p>Esta contraseña vence en 7 días. Al ingresar se te pedirá que la cambies.</p>
                        <p>Por seguridad, no compartas estas credenciales.</p>");
            }
            catch
            {
                // El email es best-effort: si falla, el usuario se creó igual.
                // La contraseña provisoria se devuelve en la respuesta para el admin.
            }

            // 9. Recargar y retornar resultado.
            var usuarioDto = await CargarUsuarioDtoAsync(usuario.IdUsuario);
            return new CrearUsuarioResultDto
            {
                Usuario             = usuarioDto,
                ContrasenaProvisoria = contrasenaProvisoria,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // CONSULTAS
        // ─────────────────────────────────────────────────────────────────────
        public async Task<UsuarioDto> ObtenerPorIdAsync(Guid id)
        {
            bool existe = await _context.Usuarios.AnyAsync(u => u.IdUsuario == id);
            if (!existe)
                throw new KeyNotFoundException($"No existe un usuario con id '{id}'.");

            return await CargarUsuarioDtoAsync(id);
        }

        public async Task<List<UsuarioDto>> ObtenerTodosAsync()
        {
            var ids = await _context.Usuarios
                .Select(u => u.IdUsuario)
                .ToListAsync();

            var resultado = new List<UsuarioDto>(ids.Count);
            foreach (var id in ids)
                resultado.Add(await CargarUsuarioDtoAsync(id));

            return resultado;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BAJA LÓGICA
        // ─────────────────────────────────────────────────────────────────────
        public async Task ActivarAsync(Guid id)
        {
            var usuario = await _context.Usuarios.FindAsync(id)
                ?? throw new KeyNotFoundException($"No existe un usuario con id '{id}'.");

            if (usuario.Activo)
                throw new InvalidOperationException("El usuario ya está activo.");

            usuario.Activo = true;
            await _context.SaveChangesAsync();
        }

        public async Task DesactivarAsync(Guid id)
        {
            var usuario = await _context.Usuarios.FindAsync(id)
                ?? throw new KeyNotFoundException($"No existe un usuario con id '{id}'.");

            if (!usuario.Activo)
                throw new InvalidOperationException("El usuario ya está inactivo.");

            usuario.Activo = false;

            // Desvincular de EspaciosCurriculares: IdDocente → null
            if (await _context.Docentes.AnyAsync(d => d.IdUsuario == id))
            {
                var idDocente = await _context.Docentes
                    .Where(d => d.IdUsuario == id)
                    .Select(d => d.IdDocente)
                    .FirstAsync();

                await _context.EspaciosCurriculares
                    .Where(ec => ec.IdDocente == idDocente)
                    .ExecuteUpdateAsync(s => s.SetProperty(ec => ec.IdDocente, (Guid?)null));
            }

            // Desvincular de Cursos: IdPreceptor → null
            if (await _context.Preceptores.AnyAsync(p => p.IdUsuario == id))
            {
                var idPreceptor = await _context.Preceptores
                    .Where(p => p.IdUsuario == id)
                    .Select(p => p.IdPreceptor)
                    .FirstAsync();

                await _context.Cursos
                    .Where(c => c.IdPreceptor == idPreceptor)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IdPreceptor, (Guid?)null));
            }

            await _context.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // VERIFICACIÓN DE UNICIDAD
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> EmailExisteAsync(string email)
            => await _context.Usuarios.AnyAsync(u => u.Email == email.Trim().ToLowerInvariant());

        public async Task<bool> DocumentoExisteAsync(string documento)
            => await _context.Usuarios.AnyAsync(u => u.Documento == documento.Trim());

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS PRIVADOS
        // ─────────────────────────────────────────────────────────────────────
        private async Task<UsuarioDto> CargarUsuarioDtoAsync(Guid id)
        {
            var u = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
                .Include(u => u.Docente)
                .Include(u => u.Preceptor)
                .FirstAsync(u => u.IdUsuario == id);

            return new UsuarioDto
            {
                IdUsuario    = u.IdUsuario,
                Nombre       = u.Nombre,
                Apellido     = u.Apellido,
                Telefono     = u.Telefono,
                Email        = u.Email,
                Documento    = u.Documento,
                Activo       = u.Activo,
                FechaCreacion = u.FechaCreacion,
                Roles        = u.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList(),
                IdDocente    = u.Docente?.IdDocente,
                IdPreceptor  = u.Preceptor?.IdPreceptor,
                EsDelegado   = u.Preceptor?.EsDelegado,
            };
        }

        private static string GenerarContrasenaAleatoria(int length = 12)
        {
            const string upper  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lower  = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string all    = upper + lower + digits;

            var bytes    = new byte[length];
            var password = new char[length];
            RandomNumberGenerator.Fill(bytes);

            // Garantizar al menos un carácter de cada tipo
            password[0] = upper[bytes[0]  % upper.Length];
            password[1] = lower[bytes[1]  % lower.Length];
            password[2] = digits[bytes[2] % digits.Length];
            for (int i = 3; i < length; i++)
                password[i] = all[bytes[i] % all.Length];

            // Shuffle Fisher-Yates con nuevos bytes
            RandomNumberGenerator.Fill(bytes);
            for (int i = length - 1; i > 0; i--)
            {
                int j = bytes[i] % (i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return new string(password);
        }
    }
}
