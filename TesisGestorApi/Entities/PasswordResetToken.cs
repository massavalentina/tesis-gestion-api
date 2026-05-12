using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class PasswordResetToken
    {
        [Key]
        public Guid Id { get; set; }

        public string Token { get; set; } = null!;

        public Guid IdUsuario { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public DateTime FechaCreacion { get; set; }
        public DateTime Expiracion { get; set; }

        public bool Usado { get; set; } = false;
    }
}
