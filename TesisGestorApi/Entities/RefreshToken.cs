using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; }

        // Token opaco: 64 bytes aleatorios en base64 (no es un JWT)
        public string Token { get; set; } = null!;

        public DateTime FechaCreacion { get; set; }
        public DateTime Expiracion { get; set; }
        public bool Revocado { get; set; } = false;

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;
    }
}
