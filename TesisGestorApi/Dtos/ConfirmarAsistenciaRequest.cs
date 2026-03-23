using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class ConfirmarAsistenciaRequest
{
    [JsonPropertyName("turno")]
    public string Turno { get; set; } = null!;

    [JsonPropertyName("attendanceTypeId")]
    public Guid TipoAsistenciaId { get; set; }

    [JsonPropertyName("studentIds")]
    public List<Guid> EstudianteIds { get; set; } = new();

    [JsonPropertyName("hora")]
    public TimeSpan? Hora { get; set; }
}
