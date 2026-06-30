namespace TesisGestorApi.DTOs
{
    public class DashboardCalificacionesDto
    {
        public decimal? AvanceProgramas { get; set; }
        public decimal? PromedioGeneral { get; set; }
        public decimal TasaAprobacionGeneral { get; set; }
        public int? AlumnosEnRiesgo { get; set; }
        public int ExamenesRealizados { get; set; }

        public decimal PorcentajeSinRecuperatorio { get; set; }
        public decimal PorcentajeConRecuperatorio1 { get; set; }
        public decimal PorcentajeConRecuperatorio2 { get; set; }

        public List<EcDesaprobacionDto> Top5EcMayorDesaprobacion { get; set; } = new();
        public List<EcPromedioDto> Top5EcMejorPromedio { get; set; } = new();
        public List<CursoTasaDesaprobacionDto> Top5CursosMayorTasa { get; set; } = new();
        public DistribucionEstadosDto DistribucionEstados { get; set; } = new();
    }

    public class EcDesaprobacionDto
    {
        public string Nombre { get; set; } = null!;
        // Tasa de desaprobación = (desaprobados + desap. por tema) / total × 100
        public decimal TasaDesaprobacion { get; set; }
    }

    public class EcPromedioDto
    {
        public string Nombre { get; set; } = null!;
        public decimal Promedio { get; set; }
    }

    public class CursoTasaDesaprobacionDto
    {
        public string Curso { get; set; } = null!;
        public decimal TasaDesaprobacion { get; set; }
    }

    public class DistribucionEstadosDto
    {
        public decimal Aprobado { get; set; }
        public decimal Desaprobado { get; set; }
        public decimal DesaprobadoPorTema { get; set; }
    }

    public class CursoLabelDto
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = null!;
    }
}
