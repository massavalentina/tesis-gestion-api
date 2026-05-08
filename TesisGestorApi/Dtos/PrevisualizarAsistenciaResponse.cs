using System.Text.Json.Serialization;

namespace TesisGestorApi.Dtos;

public class PrevisualizarAsistenciaResponse
{
    [JsonPropertyName("student")]
    public EstudianteAsistenciaDto Estudiante { get; set; } = null!;

    [JsonPropertyName("attendance")]
    public AsistenciaEscaneoDto Attendance { get; set; } = null!;
}

public class EstudianteAsistenciaDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Nombre { get; set; } = null!;

    [JsonPropertyName("lastName")]
    public string Apellido { get; set; } = null!;

    [JsonPropertyName("course")]
    public string Curso { get; set; } = null!;

    [JsonPropertyName("profileImagePath")]
    public string? FotoEstudiante { get; set; }
}

public class AsistenciaEscaneoDto
{
    [JsonPropertyName("time")]
    public string Hora { get; set; } = null!;

    [JsonPropertyName("attendanceType")]
    public string TipoAsistencia { get; set; } = null!;

    [JsonPropertyName("attendanceTypeCode")]
    public string? TipoAsistenciaCodigo { get; set; }

    [JsonPropertyName("alreadyRegisteredTurno")]
    public bool YaRegistradoEnTurno { get; set; }

    [JsonPropertyName("turno")]
    public string Turno { get; set; } = null!;
}
