using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs.Usuarios;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class UsuariosRolesService : IUsuariosRolesService
    {
        private readonly ApplicationDbContext _context;

        public UsuariosRolesService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<UsuarioConRolesDto>> GetUsuariosConRolesAsync(CancellationToken ct = default)
        {
            // Se accede a Docente y Preceptor directamente en el Select: EF genera LEFT JOINs automáticos.
            // Se ordena UsuarioRoles antes del Select para que EF pueda traducirlo a SQL.
            return await _context.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.Docente != null ? u.Docente.Apellido
                            : u.Preceptor != null ? u.Preceptor.Apellido
                            : u.Mail)
                .Select(u => new UsuarioConRolesDto(
                    u.IdUsuario,
                    u.Mail,
                    u.Docente != null ? u.Docente.Nombre : u.Preceptor != null ? u.Preceptor.Nombre : null,
                    u.Docente != null ? u.Docente.Apellido : u.Preceptor != null ? u.Preceptor.Apellido : null,
                    u.Docente != null ? u.Docente.Documento : u.Preceptor != null ? u.Preceptor.Documento : null,
                    u.Preceptor != null ? (bool?)u.Preceptor.EsDelegado : null,
                    u.UsuarioRoles
                        .OrderBy(ur => ur.Rol.Nombre)
                        .Select(ur => new RolDto(ur.Rol.IdRol, ur.Rol.Nombre))
                        .ToList()
                ))
                .ToListAsync(ct);
        }

        public async Task<List<RolDto>> GetRolesDisponiblesAsync(CancellationToken ct = default)
        {
            return await _context.Roles
                .AsNoTracking()
                .OrderBy(r => r.Nombre)
                .Select(r => new RolDto(r.IdRol, r.Nombre))
                .ToListAsync(ct);
        }

        public async Task AsignarRolAsync(Guid idUsuario, Guid idRol, CancellationToken ct = default)
        {
            var yaExiste = await _context.UsuariosRoles
                .AnyAsync(ur => ur.IdUsuario == idUsuario && ur.IdRol == idRol, ct);

            if (yaExiste) return;

            _context.UsuariosRoles.Add(new UsuarioRol
            {
                IdUsuario = idUsuario,
                IdRol = idRol
            });

            await _context.SaveChangesAsync(ct);
        }

        public async Task QuitarRolAsync(Guid idUsuario, Guid idRol, CancellationToken ct = default)
        {
            var usuarioRol = await _context.UsuariosRoles
                .FirstOrDefaultAsync(ur => ur.IdUsuario == idUsuario && ur.IdRol == idRol, ct);

            if (usuarioRol == null) return;

            _context.UsuariosRoles.Remove(usuarioRol);
            await _context.SaveChangesAsync(ct);
        }

        public async Task ActualizarDelegadoAsync(Guid idUsuario, bool esDelegado, CancellationToken ct = default)
        {
            var preceptor = await _context.Preceptores
                .FirstOrDefaultAsync(p => p.IdUsuario == idUsuario, ct);

            if (preceptor == null) return;

            preceptor.EsDelegado = esDelegado;
            await _context.SaveChangesAsync(ct);
        }
    }
}
