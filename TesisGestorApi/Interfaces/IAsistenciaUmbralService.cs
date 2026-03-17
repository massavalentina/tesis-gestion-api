namespace TesisGestorApi.Interfaces
{
    public interface IAsistenciaUmbralService
    {
        Task ProcesarUmbralesAsync(List<Guid> estudiantesIds, int anioLectivo, CancellationToken ct = default);
        Task EnviarPendientesAsync(CancellationToken ct = default);
    }
}
