using RepoDB.Entities;
using TesisGestorApi.DTOs;

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
    }
}
