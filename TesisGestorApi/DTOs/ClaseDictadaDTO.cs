namespace TesisGestorApi.DTOs
{
    public class ClaseDictadaDTO
    {
        public Guid IdEC { get; set; }
        public DateOnly Fecha { get; set; }
        public bool Dictada { get; set; } // true = Normal, false = Profesor faltó/Feriado
        public string? Tema { get; set; }
        public string? Motivo { get; set; }
    }
}
