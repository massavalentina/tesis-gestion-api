using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(ApplicationDbContext db)
        {
            var adminRol = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == "Admin");
            if (adminRol == null) return;

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

            db.UsuariosRoles.Add(new UsuarioRol { IdUsuario = adminId, IdRol = adminRol.IdRol });

            await db.SaveChangesAsync();
        }

        public static async Task NormalizarRolesAsync(ApplicationDbContext db)
        {
            var rolesValidos = new HashSet<string> { "Admin", "Docente", "Preceptor", "Equipo Directivo", "Secretario" };

            // 0. Asegurar que todos los roles válidos existen (crea los que falten)
            var rolesExistentes = (await db.Roles.Select(r => r.Nombre).ToListAsync()).ToHashSet();
            var rolesCreados = false;
            foreach (var nombre in rolesValidos)
            {
                if (!rolesExistentes.Contains(nombre))
                {
                    db.Roles.Add(new Rol { IdRol = Guid.NewGuid(), Nombre = nombre });
                    rolesCreados = true;
                }
            }
            if (rolesCreados) await db.SaveChangesAsync();

            var renombres = new Dictionary<string, string>
            {
                { "Administrador", "Admin" },
                { "Director",      "Equipo Directivo" },
            };

            // 1. Renombrar / reasignar roles obsoletos con equivalente conocido
            foreach (var (viejo, nuevo) in renombres)
            {
                var rolViejo = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == viejo);
                if (rolViejo == null) continue;

                var rolNuevo = await db.Roles.FirstOrDefaultAsync(r => r.Nombre == nuevo);

                if (rolNuevo != null)
                {
                    // Reasignar usuarios que tenían el rol viejo (sin duplicar si ya tienen el nuevo)
                    var usuariosConViejo = await db.UsuariosRoles
                        .Where(ur => ur.IdRol == rolViejo.IdRol)
                        .ToListAsync();

                    foreach (var ur in usuariosConViejo)
                    {
                        bool yaConNuevo = await db.UsuariosRoles
                            .AnyAsync(x => x.IdUsuario == ur.IdUsuario && x.IdRol == rolNuevo.IdRol);
                        if (!yaConNuevo)
                            db.UsuariosRoles.Add(new UsuarioRol { IdUsuario = ur.IdUsuario, IdRol = rolNuevo.IdRol });
                    }

                    db.UsuariosRoles.RemoveRange(usuariosConViejo);
                }
                else
                {
                    rolViejo.Nombre = nuevo;
                    continue;
                }

                var permisosViejos = await db.RolPermisos.Where(rp => rp.IdRol == rolViejo.IdRol).ToListAsync();
                db.RolPermisos.RemoveRange(permisosViejos);
                db.Roles.Remove(rolViejo);
            }

            await db.SaveChangesAsync();

            // 2. Eliminar cualquier otro rol que no esté en la lista válida
            var obsoletos = await db.Roles
                .Where(r => !rolesValidos.Contains(r.Nombre))
                .ToListAsync();

            foreach (var rol in obsoletos)
            {
                var urObs  = await db.UsuariosRoles.Where(ur => ur.IdRol == rol.IdRol).ToListAsync();
                var rpObs  = await db.RolPermisos.Where(rp => rp.IdRol == rol.IdRol).ToListAsync();
                db.UsuariosRoles.RemoveRange(urObs);
                db.RolPermisos.RemoveRange(rpObs);
                db.Roles.Remove(rol);
            }

            if (obsoletos.Count > 0)
                await db.SaveChangesAsync();

            // 3. Deduplicar roles válidos: conservar el primero existente, eliminar los demás
            foreach (var nombre in rolesValidos)
            {
                var todos = await db.Roles
                    .Where(r => r.Nombre == nombre)
                    .OrderBy(r => r.IdRol)
                    .ToListAsync();

                if (todos.Count <= 1) continue;

                var mantener   = todos.First();
                var eliminar   = todos.Skip(1).ToList();

                foreach (var dup in eliminar)
                {
                    var asignaciones = await db.UsuariosRoles
                        .Where(ur => ur.IdRol == dup.IdRol)
                        .ToListAsync();

                    foreach (var ur in asignaciones)
                    {
                        bool yaLoTiene = await db.UsuariosRoles
                            .AnyAsync(x => x.IdUsuario == ur.IdUsuario && x.IdRol == mantener.IdRol);
                        if (!yaLoTiene)
                            db.UsuariosRoles.Add(new UsuarioRol { IdUsuario = ur.IdUsuario, IdRol = mantener.IdRol });
                    }

                    await db.SaveChangesAsync(); // primero insertar antes de borrar

                    var rpDup = await db.RolPermisos.Where(rp => rp.IdRol == dup.IdRol).ToListAsync();
                    db.UsuariosRoles.RemoveRange(asignaciones);
                    db.RolPermisos.RemoveRange(rpDup);
                    db.Roles.Remove(dup);
                    await db.SaveChangesAsync();
                }
            }
        }

        public static async Task SeedDevUsuariosAsync(ApplicationDbContext db)
        {
            const string pwd = "Dev1234";

            var devUsuarios = new[]
            {
                (email: "admin@dev.local",      nombre: "Admin",      apellido: "Dev", doc: "99000001", rol: "Admin"),
                (email: "docente@dev.local",     nombre: "Docente",    apellido: "Dev", doc: "99000002", rol: "Docente"),
                (email: "preceptor@dev.local",   nombre: "Preceptor",  apellido: "Dev", doc: "99000003", rol: "Preceptor"),
                (email: "secretario@dev.local",  nombre: "Secretario", apellido: "Dev", doc: "99000004", rol: "Secretario"),
                (email: "directivo@dev.local",   nombre: "Directivo",  apellido: "Dev", doc: "99000005", rol: "Equipo Directivo"),
            };

            var roles = await db.Roles.ToListAsync();

            foreach (var u in devUsuarios)
            {
                if (await db.Usuarios.AnyAsync(x => x.Email == u.email))
                    continue;

                var rol = roles.FirstOrDefault(r => r.Nombre == u.rol);
                if (rol == null) continue;

                var id = Guid.NewGuid();

                db.Usuarios.Add(new Usuario
                {
                    IdUsuario                  = id,
                    Nombre                     = u.nombre,
                    Apellido                   = u.apellido,
                    Email                      = u.email,
                    Documento                  = u.doc,
                    Contraseña                 = BCrypt.Net.BCrypt.HashPassword(pwd, workFactor: 4),
                    Activo                     = true,
                    FechaCreacion              = DateTime.UtcNow,
                    IntentosFailidos           = 0,
                    RequiereCambioContrasena   = false,
                });

                db.UsuariosRoles.Add(new UsuarioRol { IdUsuario = id, IdRol = rol.IdRol });

                if (u.rol == "Docente")
                    db.Docentes.Add(new Docente { IdDocente = Guid.NewGuid(), IdUsuario = id });

                if (u.rol == "Preceptor")
                    db.Preceptores.Add(new Preceptor { IdPreceptor = Guid.NewGuid(), IdUsuario = id });
            }

            await db.SaveChangesAsync();
        }
    }
}
