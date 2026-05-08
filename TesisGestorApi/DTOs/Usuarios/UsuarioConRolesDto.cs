namespace TesisGestorApi.DTOs.Usuarios
{
    public record UsuarioConRolesDto(
        Guid IdUsuario,
        string Mail,
        string? Nombre,
        string? Apellido,
        string? Documento,
        bool? EsDelegado,
        List<RolDto> Roles
    );
}
