using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(ApplicationDbContext db)
        {
            var adminRol = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == "Admin");
            if (adminRol == null) return; // roles no sembrados aún

            // No crear si el usuario admin ya existe (por email)
            if (await db.Usuarios.AnyAsync(u => u.Email == "admin@sistema.local")) return;

            const string pwdInicial = "Admin@1234";
            var hash    = BCrypt.Net.BCrypt.HashPassword(pwdInicial, workFactor: 12);
            var adminId = Guid.NewGuid();

            db.Usuarios.Add(new Usuario
            {
                IdUsuario                  = adminId,
                Nombre                     = "Admin",
                Apellido                   = "Sistema",
                Email                      = "admin@sistema.local",
                Documento                  = "00000000",
                Contraseña                 = hash,
                Activo                     = true,
                FechaCreacion              = DateTime.UtcNow,
                IntentosFailidos           = 0,
                RequiereCambioContrasena   = true,
                FechaVencimientoContrasena = DateTime.UtcNow.AddDays(30),
            });

            db.UsuariosRoles.Add(new UsuarioRol
            {
                IdUsuario = adminId,
                IdRol     = adminRol.IdRol,
            });

            await db.SaveChangesAsync();
        }
    }
}
