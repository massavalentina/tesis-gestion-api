using Microsoft.EntityFrameworkCore;
using TesisGestorApi.DTOs;



namespace TesisGestorApi.Interfaces
{
    /// <summary>
    ///  Interfaz de servicio para poder aplicar polimorfismo sobre el cálculo de asistencia. La interfaz define dos métodos de cálculo, uno para registros individuales y otra en lotes. 
    ///  Cada clase que implemente esta interfaz deberá sobreescribir ambos métodos.
    /// </summary>
    public interface IAsistenciaService
    {
        Task<int> RegistrarLoteAsync(List<RegistrarAsistenciaDto> listaDto);

        // Registro de Asistencia General Individual
        /// Recibe una asistencia y la procesa con el método de procesamiento por lote.
        Task<AsistenciaResponseDto> RegistrarAsistenciaIndividualAsync(RegistrarAsistenciaDto dto);

        // Obtiene todas las asistencias con parámetros de Fecha y Estudiante opcionales.
        Task<IEnumerable<AsistenciaGetDTO>> ObtenerAsistenciasAsync(DateOnly? fecha, Guid? estudianteId);

        // Actualiza el estado de una clase (Dictada o No Dictada)
        Task ActualizarEstadoClaseAsync(ClaseDictadaDTO dto);

        // Regenera las asistencias de una clase específica en base al estado de Dictado (true o false) y recalcula en base a la data de asistencias. 
        Task RegenerarAsistenciasParaClase(ClaseDictada clase);

        Task<AsistenciaResponseDto> DeshacerAsistenciaRapidaAsync(DeshacerAsistenciaRapidaDto dto);

    }
}
