using TesisGestorApi.DTOs.Planificaciones;

namespace TesisGestorApi.Interfaces;

public interface IPlanificacionService
{
    Task<ArbolPlanificacionDto> GetArbolAsync(Guid idEC, Guid idDocente, CancellationToken ct);

    Task<UnidadArbolDto> CrearUnidadAsync(Guid idEC, Guid idDocente, CrearItemArchivoDto dto, CancellationToken ct);

    Task<TemaArbolDto> CrearTemaAsync(Guid idEC, Guid idUnidad, Guid idDocente, CrearItemArchivoDto dto, CancellationToken ct);

    Task<ClasePlanificacionDto> CrearClaseAsync(Guid idEC, Guid idDocente, CrearClaseDto dto, string? urlArchivo, CancellationToken ct);

    Task<ClasePlanificacionDto> EditarClaseAsync(Guid idClase, Guid idDocente, EditarClaseDto dto, string? urlArchivo, CancellationToken ct);

    Task CambiarEstadoClaseAsync(Guid idClase, Guid idDocente, string nuevoEstado, CancellationToken ct);

    Task EliminarClaseAsync(Guid idClase, Guid idDocente, CancellationToken ct);

    Task CambiarEstadoBloqueAsync(Guid idBloque, Guid idDocente, string nuevoEstado, CancellationToken ct);
}
