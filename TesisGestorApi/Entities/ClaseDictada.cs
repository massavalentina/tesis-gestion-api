
using System.ComponentModel.DataAnnotations;
namespace TesisGestorApi.Entities
{
    public class ClaseDictada
    {
        [Key]
        public Guid IdClaseDictada { get; set; }
        public DateOnly Fecha { get; set; }
        public string? Tema { get; set; }

        // Flag para las asistencias al espacio curricular 
        // True = Clase normal. False = Profesor ausente / Feriado / Jornada inst.
        public bool Dictada { get; set; }
        public Guid IdEC { get; set; }
        public EspacioCurricular EspacioCurricular { get; set; } = null!;

        public ICollection<AsistenciaPorEspacio> Asistencias { get; set; } = new List<AsistenciaPorEspacio>();
    }
}
