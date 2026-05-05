using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class PrevisualizarAsistenciaRequest
{
    [Required]
    [JsonPropertyName("qr")]
    public string CodigoQr { get; set; } = null!;

    // Campo legado para compatibilidad; scanner rediseñado ya no usa curso.
    [JsonPropertyName("idCurso")]
    public Guid IdCurso { get; set; }

    // Turno opcional; si no viene se resuelve automáticamente por hora de servidor.
    [JsonPropertyName("turno")]
    public string? Turno { get; set; }
}
