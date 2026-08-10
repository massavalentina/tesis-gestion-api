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
        public List<AnioTasaAprobacionDto> TasaAprobacionPorAnio { get; set; } = new();
        public List<CursoTasaAprobacionDto> TasaAprobacionPorCurso { get; set; } = new();
    }

    public class EcDesaprobacionDto
    {
        public string Nombre { get; set; } = null!;
        // % de alumnos con promedio < 7 en esta materia, sobre el total de
        // alumnos evaluados en ella (nivel alumno, no tema individual).
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
        // Promedio simple de las TasaDesaprobacion (por alumno) de las
        // materias que dicta este curso — cada materia pesa igual.
        public decimal TasaDesaprobacion { get; set; }
    }

    /// <summary>
    /// Distribución calculada por alumno + materia (EC), sobre el promedio de sus
    /// notas de instancia (máximo entre original y recuperatorios).
    /// Tres estados MUTUAMENTE EXCLUYENTES; solo "Aprobado" representa aprobación real.
    /// Aprobado = promedio >= 7 sin ningún tema individual desaprobado.
    /// DesaprobadoPorTema = promedio >= 7 pero con algún tema individual < 7
    /// (el promedio da bien, pero el alumno NO aprobó: desaprobó un tema).
    /// Desaprobado = promedio < 7.
    /// </summary>
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

    public class AnioTasaAprobacionDto
    {
        public int Anio { get; set; }
        public decimal TasaAprobacion { get; set; }
    }

    public class CursoTasaAprobacionDto
    {
        public string Curso { get; set; } = null!;
        public decimal TasaAprobacion { get; set; }
    }
}
