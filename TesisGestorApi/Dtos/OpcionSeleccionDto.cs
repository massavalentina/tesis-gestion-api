using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class OpcionSeleccionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;
}
