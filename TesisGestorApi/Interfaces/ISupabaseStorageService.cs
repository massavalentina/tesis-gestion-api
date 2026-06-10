namespace TesisGestorApi.Interfaces;

public interface ISupabaseStorageService
{
    Task<string> SubirArchivoAsync(Stream contenido, string rutaRelativa, string contentType, CancellationToken ct);
    string GetUrlPublica(string rutaRelativa);
}
