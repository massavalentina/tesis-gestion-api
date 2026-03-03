using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class EscanearAsistenciaRequest
{
    [JsonPropertyName("qr")]
    public Guid CodigoQr { get; set; }

    [JsonPropertyName("turno")]
    public string Turno { get; set; } = null!;

    [JsonPropertyName("attendanceTypeId")]
    public Guid TipoAsistenciaId { get; set; }
}
