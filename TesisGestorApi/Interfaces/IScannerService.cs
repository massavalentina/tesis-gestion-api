using TesisGestorApi.Dtos;

namespace TesisGestorApi.Interfaces
{
    public interface IScannerService
    {
        Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request);
        Task ConfirmarAsync(ConfirmarAsistenciaRequest request);
        Task<List<OpcionSeleccionDto>> ObtenerCursosScannerAsync();
        List<OpcionSeleccionDto> ObtenerTurnos();
        Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync();
    }
}
