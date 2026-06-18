using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services;

public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _key;
    private readonly string _bucket;

    public SupabaseStorageService(HttpClient http, IConfiguration config)
    {
        _http   = http;
        _url    = config["Supabase:Url"]    ?? throw new InvalidOperationException("Supabase:Url no configurado.");
        _key    = config["Supabase:Key"]    ?? throw new InvalidOperationException("Supabase:Key no configurado.");
        _bucket = config["Supabase:Bucket"] ?? throw new InvalidOperationException("Supabase:Bucket no configurado.");
    }

    public async Task<string> SubirArchivoAsync(Stream contenido, string rutaRelativa, string contentType, CancellationToken ct)
    {
        var endpoint = $"{_url}/storage/v1/object/{_bucket}/{rutaRelativa}";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("Authorization", $"Bearer {_key}");
        request.Content = new StreamContent(contenido);
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Error al subir el archivo a Supabase Storage ({(int)response.StatusCode}): {body}");
        }

        return rutaRelativa;
    }

    public async Task EliminarArchivoAsync(string rutaRelativa, CancellationToken ct)
    {
        var endpoint = $"{_url}/storage/v1/object/{_bucket}/{rutaRelativa}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        request.Headers.Add("Authorization", $"Bearer {_key}");

        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Error al eliminar el archivo de Supabase Storage ({(int)response.StatusCode}): {body}");
        }
    }

    public string GetUrlPublica(string rutaRelativa)
        => $"{_url}/storage/v1/object/public/{_bucket}/{rutaRelativa}";
}
