namespace TesisGestorApi.DTOs.Auth
{
    public record DevLoginUsuarioDto(Guid IdUsuario, string Mail, List<string> Roles);
}
