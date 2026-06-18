using TesisGestorApi.DTOs.Calificaciones;
using TesisGestorApi.Entities;

namespace TesisGestorApi.Interfaces
{
    public interface ICalificacionesWriteService
    {
        Task<CalificacionesApplyResult> ApplyChangesAsync(
            CalificacionesApplyRequest request,
            CancellationToken ct);
    }

    public sealed record CalificacionesApplyRequest(
        Guid IdEC,
        Guid IdUsuario,
        string DocenteLabel,
        OrigenCarga Origen,
        Guid? IdImportacionCalificaciones,
        IReadOnlyCollection<CalificacionApplyChange> Cambios);

    public sealed record CalificacionApplyChange(
        Guid IdIE,
        Guid IdEstudiante,
        TipoCalificacion TipoCalificacion,
        int? Puntaje);

    public sealed record CalificacionesApplyResult(
        int CambiosAplicados,
        IReadOnlyList<Guid> InstanciasAfectadas,
        AuditoriaCalificacionSesionDto? SesionAuditoria);
}
