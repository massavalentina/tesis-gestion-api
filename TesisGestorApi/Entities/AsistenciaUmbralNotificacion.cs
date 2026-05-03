
using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class AsistenciaUmbralNotificacion
    {
        [Key]
        public Guid IdNotif { get; set; }

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public int AnioLectivo { get; set; }
        public int Umbral { get; set; } // 10/15/20/25

        public DateTime CreadoUtc { get; set; }
    }
}
