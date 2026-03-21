
using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.Entities
{
    public class AsistenciaResumenAnual
    {
        [Key]
        public Guid IdResumen { get; set; }

        public Guid IdEstudiante { get; set; }
        public Estudiante Estudiante { get; set; } = null!;

        public int AnioLectivo { get; set; }

        public decimal FaltasAcumuladas { get; set; } = 0m;
        public DateTime UltimoRecalculoUtc { get; set; }

        public bool TeaGeneral { get; set; } = false;
        public DateTime? FechaTeaGeneralUtc { get; set; }

    }
}
