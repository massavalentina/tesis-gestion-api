using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Services
{

    /// <summary>
    ///  Interfaz de servicio para poder aplicar polimorfismo sobre el cálculo de asistencia. La interfaz define dos métodos de cálculo, uno para registros individuales y otra en lotes. 
    ///  Cada clase que implemente esta interfaz deberá sobreescribir ambos métodos.
    /// </summary>
    public interface IAsistenciaService
    {
        Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> listaDto);
        Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto);
        Task<IEnumerable<AsistenciaGetDTO>> ObtenerAsistenciasAsync(DateOnly? fecha, Guid? estudianteId);

    }
}
