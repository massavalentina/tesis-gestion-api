using TesisGestorApi.Interfaces;

namespace TesisGestorApi.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor accessor)
        {
            _httpContextAccessor = accessor;
        }

        public Guid? UserId
        {
            get
            {
                var val = _httpContextAccessor.HttpContext?.User?.FindFirst("idUsuario")?.Value;
                return val != null && Guid.TryParse(val, out var id) ? id : null;
            }
        }

        public string NombreCompleto =>
            _httpContextAccessor.HttpContext?.User?.FindFirst("nombre")?.Value
            ?? "Sistema";
    }
}
