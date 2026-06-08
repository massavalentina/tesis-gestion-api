namespace TesisGestorApi.DTOs.Asignaciones
{
    public record MisEcItemDto(
        Guid IdEC,
        Guid IdCurso,
        string NombreMateria,
        string CodigoCurso,
        int AnioNumero,
        string Division,
        int AnioLectivo,
        int CantidadEstudiantes,
        List<HorarioDto> Horarios
    );
}
