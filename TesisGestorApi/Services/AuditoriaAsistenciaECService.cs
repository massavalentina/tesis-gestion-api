using Microsoft.EntityFrameworkCore;
using TesisGestorApi.Data;
using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;
using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class AuditoriaAsistenciaECService : IAuditoriaAsistenciaECService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService  _currentUser;

        public AuditoriaAsistenciaECService(ApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context     = context;
            _currentUser = currentUser;
        }

        public async Task RegistrarAsync(
            Guid estudianteId,
            Guid idClaseDictada,
            TipoEventoAuditoriaEC tipoEvento,
            bool? estadoAnterior,
            bool estadoNuevo,
            TimeSpan horarioEvento)
        {
            Guid userId = _currentUser.UserId ?? Guid.Empty;
            _context.AuditoriasAsistenciaEC.Add(new AuditoriaAsistenciaEC
            {
                IdAuditoria    = Guid.NewGuid(),
                IdEstudiante   = estudianteId,
                IdClaseDictada = idClaseDictada,
                TipoEvento     = tipoEvento,
                EstadoAnterior = estadoAnterior,
                EstadoNuevo    = estadoNuevo,
                HorarioEvento  = horarioEvento,
                IdUsuario      = userId,
                FechaRegistro  = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
        }

        public async Task RegistrarLoteAsync(
            IEnumerable<(Guid EstudianteId, Guid IdClaseDictada, bool? EstadoAnterior, bool EstadoNuevo, TimeSpan HorarioEvento)> cambios,
            TipoEventoAuditoriaEC tipoEvento)
        {
            var lista = cambios.ToList();
            if (!lista.Any()) return;

            Guid userId = _currentUser.UserId ?? Guid.Empty;
            DateTime ahora = DateTime.UtcNow;

            var registros = lista.Select(c => new AuditoriaAsistenciaEC
            {
                IdAuditoria    = Guid.NewGuid(),
                IdEstudiante   = c.EstudianteId,
                IdClaseDictada = c.IdClaseDictada,
                TipoEvento     = tipoEvento,
                EstadoAnterior = c.EstadoAnterior,
                EstadoNuevo    = c.EstadoNuevo,
                HorarioEvento  = c.HorarioEvento,
                IdUsuario      = userId,
                FechaRegistro  = ahora,
            }).ToList();

            _context.AuditoriasAsistenciaEC.AddRange(registros);
            await _context.SaveChangesAsync();
        }

        public async Task<List<AuditoriaAsistenciaECDto>> ObtenerPorEstudianteFechaAsync(Guid estudianteId, DateOnly fecha)
        {
            return await _context.AuditoriasAsistenciaEC
                .AsNoTracking()
                .Include(a => a.ClaseDictada)
                    .ThenInclude(cd => cd.EspacioCurricular)
                        .ThenInclude(ec => ec.Curricula)
                .Include(a => a.Usuario)
                .Where(a => a.IdEstudiante == estudianteId && a.ClaseDictada.Fecha == fecha)
                .OrderBy(a => a.FechaRegistro)
                .Select(a => new AuditoriaAsistenciaECDto
                {
                    IdAuditoria      = a.IdAuditoria,
                    TipoEvento       = (int)a.TipoEvento,
                    TipoEventoLabel  = a.TipoEvento == TipoEventoAuditoriaEC.RegistroGeneral   ? "Registro General"
                                     : a.TipoEvento == TipoEventoAuditoriaEC.Retiro            ? "Retiro"
                                     : a.TipoEvento == TipoEventoAuditoriaEC.CancelacionRetiro ? "Cancelación de Retiro"
                                     : "Cambio Manual",
                    NombreMateria    = a.ClaseDictada.EspacioCurricular.Curricula.Nombre,
                    EstadoAnterior   = a.EstadoAnterior,
                    EstadoNuevo      = a.EstadoNuevo,
                    HorarioEvento    = a.HorarioEvento.ToString(@"hh\:mm"),
                    FechaRegistro    = a.FechaRegistro,
                    NombreUsuario    = a.Usuario.Nombre,
                    ApellidoUsuario  = a.Usuario.Apellido,
                })
                .ToListAsync();
        }
    }
}
