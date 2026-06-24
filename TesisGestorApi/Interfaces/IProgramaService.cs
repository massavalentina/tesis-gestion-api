using TesisGestorApi.DTOs.Programas;

namespace TesisGestorApi.Interfaces
{
    public interface IProgramaService
    {
        Task<List<ProgramaResumenDto>> GetProgramasPorECAsync(Guid idEC, Guid idDocente, CancellationToken ct);
        Task<ProgramaDetalleDto> GetProgramaAsync(Guid idPrograma, Guid idDocente, CancellationToken ct);
        Task<ProgramaDetalleDto> CrearProgramaAsync(Guid idDocente, CrearProgramaDto dto, CancellationToken ct);
        Task<ProgramaDetalleDto> ActualizarProgramaAsync(Guid idPrograma, Guid idDocente, CrearProgramaDto dto, CancellationToken ct);
        Task CambiarEstadoAsync(Guid idPrograma, Guid idDocente, string nuevoEstado, CancellationToken ct);
        Task EliminarProgramaAsync(Guid idPrograma, Guid idDocente, CancellationToken ct);
        Task<ProgramaDetalleDto> CrearDesdeArchivoAsync(Guid idDocente, CargarProgramaArchivoDto dto, string urlArchivo, CancellationToken ct);
    }
}
