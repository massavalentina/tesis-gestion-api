using TesisGestorApi.DTOs;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Interfaces
{
    public interface IAuditoriaAsistenciaECService
    {
        /// <summary>Registra un único evento de auditoría (usado para cambios manuales, evento 3).</summary>
        Task RegistrarAsync(
            Guid estudianteId,
            Guid idClaseDictada,
            TipoEventoAuditoriaEC tipoEvento,
            bool? estadoAnterior,
            bool estadoNuevo,
            TimeSpan horarioEvento);

        /// <summary>Registra un lote de eventos del mismo tipo (usado para RegistroGeneral y Retiro).</summary>
        Task RegistrarLoteAsync(
            IEnumerable<(Guid EstudianteId, Guid IdClaseDictada, bool? EstadoAnterior, bool EstadoNuevo, TimeSpan HorarioEvento)> cambios,
            TipoEventoAuditoriaEC tipoEvento);

        /// <summary>Obtiene todos los eventos de auditoría de un estudiante para una fecha dada.</summary>
        Task<List<AuditoriaAsistenciaECDto>> ObtenerPorEstudianteFechaAsync(Guid estudianteId, DateOnly fecha);
    }
}
