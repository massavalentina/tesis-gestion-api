namespace TesisGestorApi.DTOs.ParteDiario
{
    public class HorarioClaseDto
    {
        public Guid IdEC { get; set; }
        public Guid? IdClaseDictada { get; set; }
        public string Materia { get; set; } = null!;
        public string Docente { get; set; } = null!;
        public string HoraEntrada { get; set; } = null!;
        public string HoraSalida { get; set; } = null!;

        /// <summary>Presentes solo cuando la clase fue movida de su horario original.</summary>
        public string? HoraEntradaOriginal { get; set; }
        public string? HoraSalidaOriginal  { get; set; }

        /// <summary>null = sin registro de clase dictada para ese día</summary>
        public bool? Dictada { get; set; }
        public string? Motivo { get; set; }
        public string? Tema { get; set; }
    }
}
