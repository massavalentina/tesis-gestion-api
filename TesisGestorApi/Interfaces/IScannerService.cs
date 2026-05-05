using TesisGestorApi.Dtos;

namespace TesisGestorApi.Interfaces
{
    public interface IScannerService
    {
        Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request);
        Task ConfirmarAsync(ConfirmarAsistenciaRequest request);
        TurnoSesionResponse ObtenerTurnoSesion(string? turno);
        List<OpcionSeleccionDto> ObtenerTurnos();
        Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync();
    }
}
