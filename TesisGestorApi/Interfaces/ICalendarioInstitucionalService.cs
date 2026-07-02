using TesisGestorApi.DTOs.Calendario;

namespace TesisGestorApi.Interfaces
{
    public interface ICalendarioInstitucionalService
    {
        Task<List<EventoInstitucionalDto>> ObtenerEventosAsync(int anioLectivo, Guid? idUsuario, List<string> roles, bool esAdmin, CancellationToken ct);
        Task<List<object>> ObtenerCursosUsuarioAsync(int anioLectivo, Guid? idUsuario, List<string> roles, bool esAdmin, CancellationToken ct);
        Task<EventoInstitucionalDto> ObtenerPorIdAsync(Guid idEvento, CancellationToken ct);
        Task<EventoInstitucionalDto> CrearEventoAsync(CrearEventoInstitucionalDto dto, CancellationToken ct);
        Task<EventoInstitucionalDto> ActualizarEventoAsync(Guid idEvento, ActualizarEventoInstitucionalDto dto, CancellationToken ct);
        Task EliminarEventoAsync(Guid idEvento, CancellationToken ct);
        Task<List<AuditoriaEventoDto>> ObtenerAuditoriaEventoAsync(Guid idEvento, CancellationToken ct);
        Task<List<AuditoriaEventoDto>> ObtenerAuditoriaGeneralAsync(int anioLectivo, CancellationToken ct);
    }
}
