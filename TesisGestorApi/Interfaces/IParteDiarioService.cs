using TesisGestorApi.DTOs.ParteDiario;

namespace TesisGestorApi.Interfaces
{
    public interface IParteDiarioService
    {
        Task<ParteDiarioResumenDto> ObtenerResumenAsync(Guid cursoId, DateOnly fecha);
        Task<List<ComentarioParteDto>> ObtenerComentariosAsync(Guid cursoId, DateOnly fecha);
        Task<ComentarioParteDto> AgregarComentarioAsync(AgregarComentarioDto dto);
        Task RegistrarEventoAsync(Guid cursoId, DateOnly fecha, string descripcion);
        Task IntercambiarHorarioClasesAsync(IntercambiarHorarioDto dto);
        Task ResetearHorarioClaseAsync(Guid idEC, DateOnly fecha, Guid cursoId);
        Task ReorganizarHorarioAsync(ReorganizarHorarioDto dto);
    }
}
