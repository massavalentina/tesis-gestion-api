using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class PrevisualizarAsistenciaRequest
{
    [Required]
    [JsonPropertyName("qr")]
    public string CodigoQr { get; set; } = null!;

    [Required]
    [JsonPropertyName("idCurso")]
    public Guid IdCurso { get; set; }

    [Required]
    [JsonPropertyName("turno")]
    public string Turno { get; set; } = null!;
}
