using TesisGestorApi.DTOs;
using TesisGestorApi.Dtos;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Interfaces
{
    public interface IAsistenciaService
    {
        Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> listaDto);

        Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto);

        Task<IEnumerable<AsistenciaGetDTO>> ObtenerAsistenciasAsync(DateOnly? fecha, Guid? estudianteId);

        Task ActualizarEstadoClaseAsync(ClaseDictadaDTO dto);

        Task RegenerarAsistenciasParaClase(ClaseDictada clase);

        Task<AsistenciaResponseDto> DeshacerAsistenciaRapidaAsync(DeshacerAsistenciaRapidaDto dto);

        Task<PrevisualizarAsistenciaResponse> PrevisualizarAsync(PrevisualizarAsistenciaRequest request);

        Task ConfirmarAsync(ConfirmarAsistenciaRequest request);

        Task<List<OpcionSeleccionDto>> ObtenerCursosAsync();

        List<OpcionSeleccionDto> ObtenerTurnos();

        Task<List<OpcionSeleccionDto>> ObtenerTiposAsistenciaAsync();

        Task<List<AsistenciaEspacioItemDto>> ObtenerAsistenciaEspaciosDiaAsync(Guid estudianteId, DateOnly fecha);

        Task ActualizarAsistenciaEspacioAsync(ActualizarAsistenciaEspacioDto dto);
        Task RecalcularAsistenciasCursoFechaAsync(Guid cursoId, DateOnly fecha);
    }
}
