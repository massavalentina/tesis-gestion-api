namespace TesisGestorApi.DTOs.ParteDiario
{
    public class SlotReorganizadoDto
    {
        public Guid   IdHorario   { get; set; }
        /// <summary>Hora de inicio efectiva elegida por el preceptor, formato "HH:mm".</summary>
        public string HoraEntrada { get; set; } = null!;
        /// <summary>Hora de fin efectiva elegida por el preceptor, formato "HH:mm".</summary>
        public string HoraSalida  { get; set; } = null!;
    }

    public class ReorganizarHorarioDto
    {
        public Guid   CursoId { get; set; }
        public DateOnly Fecha { get; set; }
        /// <summary>Un slot por clase del turno, con los tiempos efectivos definidos por el preceptor.</summary>
        public List<SlotReorganizadoDto> Slots { get; set; } = new();
    }
}
