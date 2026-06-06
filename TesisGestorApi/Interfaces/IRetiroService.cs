using TesisGestorApi.DTOs.Retiro;

namespace TesisGestorApi.Interfaces
{
    public interface IRetiroService
    {
        Task<List<TutorEstudianteDto>> ObtenerTutoresEstudianteAsync(Guid estudianteId);
        Task<List<RetiroActivoDto>> ObtenerRetirosActivosAsync(Guid estudianteId, DateOnly fecha);
        Task<RetiroActivoDto> RegistrarRetiroAsync(RegistrarRetiroDto dto);
        Task<RetiroActivoDto> RegistrarReingresoAsync(RegistrarReingresoDto dto);
        Task<RetiroActivoDto> ActualizarRetiroAsync(Guid idRetiro, ActualizarRetiroDto dto);
        Task CancelarRetiroAsync(Guid idRetiro, string? motivo);
    }
}
