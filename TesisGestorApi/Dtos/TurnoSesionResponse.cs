using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class TurnoSesionResponse
{
    [JsonPropertyName("turno")]
    public string Turno { get; set; } = null!;

    [JsonPropertyName("serverTime")]
    public string ServerTime { get; set; } = null!;

    [JsonPropertyName("cutoffTime")]
    public string CutoffTime { get; set; } = null!;
}
