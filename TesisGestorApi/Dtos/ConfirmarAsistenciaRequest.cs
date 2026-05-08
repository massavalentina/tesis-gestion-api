using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class ConfirmarAsistenciaRequest
{
    // Contrato nuevo: lista consolidada final por alumno-turno.
    [JsonPropertyName("items")]
    public List<ConfirmarAsistenciaItemRequest> Items { get; set; } = new();

    // Campos legacy para compatibilidad temporal.
    [JsonPropertyName("turno")]
    public string? Turno { get; set; }

    [JsonPropertyName("attendanceTypeId")]
    public Guid TipoAsistenciaId { get; set; }

    [JsonPropertyName("studentIds")]
    public List<Guid> EstudianteIds { get; set; } = new();

    [JsonPropertyName("hora")]
    public TimeSpan? Hora { get; set; }
}

public class ConfirmarAsistenciaItemRequest
{
    [JsonPropertyName("studentId")]
    public Guid EstudianteId { get; set; }

    [JsonPropertyName("attendanceTypeId")]
    public Guid TipoAsistenciaId { get; set; }

    [JsonPropertyName("turno")]
    public string? Turno { get; set; }
}
