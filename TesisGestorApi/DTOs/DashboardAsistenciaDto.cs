namespace TesisGestorApi.DTOs
{
    public class DashboardAsistenciaDto
    {
        // Cards KPI
        public decimal PorcentajeAsistenciaGeneral { get; set; }
        public decimal PorcentajeAsistenciaPorEC { get; set; }
        public decimal PromedioInasistenciasPorCurso { get; set; }
        public decimal PorcentajeLlegadasTarde { get; set; }
        public decimal PorcentajeRetirosAnticipados { get; set; }
        public int AlumnosTeaGeneral { get; set; }
        public int AlumnosTeaPorEspacio { get; set; }

        // Gráfico barras: Promedio inasistencias por curso
        public List<CursoInasistenciaDto> InasistenciasPorCurso { get; set; } = new();

        // Gráfico barras: % asistencia por EC
        public List<EcAsistenciaDto> AsistenciaPorEC { get; set; } = new();

        // Gráfico líneas: tendencia mensual de inasistencias
        public List<TendenciaMensualDto> TendenciaMensual { get; set; } = new();

        // Gráfico torta: distribución tipos de inasistencia
        public DistribucionInasistenciasDto DistribucionInasistencias { get; set; } = new();

        // Gráfico torta: distribución por subtipo (LLT/LLTE/LLTC o RE/RA/RAE)
        public DistribucionSubtiposDto DistribucionSubtipos { get; set; } = new();

        // Gráfico torta EC: distribución de inasistencias en espacios curriculares
        public DistribucionInasistenciasECDto DistribucionInasistenciasEC { get; set; } = new();
    }

    public class CursoInasistenciaDto
    {
        public string Curso { get; set; } = null!;
        public decimal Promedio { get; set; }
    }

    public class EcAsistenciaDto
    {
        public string NombreEC { get; set; } = null!;
        public decimal PorcentajeAsistencia { get; set; }
    }

    public class TendenciaMensualDto
    {
        public string Curso { get; set; } = null!;
        // Valores mensuales Marzo(0) a Diciembre(9), null si no hay datos
        public List<decimal?> ValoresMensuales { get; set; } = new();
    }

    public class DistribucionInasistenciasDto
    {
        public decimal Ausentes { get; set; }
        public decimal LlegadasTarde { get; set; }
        public decimal RetirosAnticipados { get; set; }
    }

    public class DistribucionSubtiposDto
    {
        // Llegadas tarde por subtipo (conteo en ambos turnos)
        public int LLT { get; set; }
        public int LLTE { get; set; }
        public int LLTC { get; set; }

        // Retiros por subtipo (conteo en ambos turnos)
        public int RE { get; set; }
        public int RA { get; set; }
        public int RAE { get; set; }
    }

    public class DistribucionInasistenciasECDto
    {
        public int Ausencias { get; set; }
        public int LlegadasTarde { get; set; }
        public int RetirosAnticipados { get; set; }
    }
}
