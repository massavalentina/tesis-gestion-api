using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TesisGestorApi.DTOs.CalificacionesImportacion
{
    public class AnalizarImportacionCalificacionesDto
    {
        [Required]
        public IFormFile Archivo { get; set; } = null!;
    }

    public class ConfirmarImportacionCalificacionesDto
    {
        [Required]
        public IFormFile Archivo { get; set; } = null!;

        [Required]
        public string PayloadJson { get; set; } = null!;
    }

    public class ConfirmarImportacionCalificacionesPayloadDto
    {
        [Required]
        public string HashArchivoSha256 { get; set; } = null!;

        [Required]
        public List<ConfirmarImportacionRevisionRowDto> Rows { get; set; } = new();
    }

    public class ConfirmarImportacionRevisionRowDto
    {
        [Required]
        public string RowId { get; set; } = null!;
        public Guid? EstudianteAsociadoId { get; set; }
        public List<ConfirmarImportacionRevisionCellDto> Cells { get; set; } = new();
    }

    public class ConfirmarImportacionRevisionCellDto
    {
        [Required]
        public string SlotKey { get; set; } = null!;

        [Required]
        public string Resolucion { get; set; } = null!;

        public Guid? IdCalificacionBase { get; set; }
    }

    public class ImportacionCalificacionesAnalisisDto
    {
        public string Estado { get; set; } = null!;
        public string NombreArchivoOriginal { get; set; } = null!;
        public string HashArchivoSha256 { get; set; } = null!;
        public ImportacionContextoDto Contexto { get; set; } = new();
        public ImportacionAnalisisResumenDto Resumen { get; set; } = new();
        public List<ImportacionIssueDto> Bloqueos { get; set; } = new();
        public List<ImportacionStudentOptionDto> EstudiantesCurso { get; set; } = new();
        public List<ImportacionSlotDto> Slots { get; set; } = new();
        public List<ImportacionRevisionRowDto> Rows { get; set; } = new();
        public ImportacionConfirmacionResumenDto ResumenConfirmacionInicial { get; set; } = new();
        public bool PuedeConfirmar { get; set; }
    }

    public class ImportacionContextoDto
    {
        public Guid IdEC { get; set; }
        public Guid IdCurso { get; set; }
        public string NombreMateria { get; set; } = null!;
        public string CodigoCurso { get; set; } = null!;
        public int AnioNumero { get; set; }
        public string Division { get; set; } = null!;
        public int AnioLectivo { get; set; }
    }

    public class ImportacionAnalisisResumenDto
    {
        public int EstudiantesDetectados { get; set; }
        public int EstudiantesSinConflicto { get; set; }
        public int EstudiantesConConflicto { get; set; }
        public int EvaluacionesDetectadasConNotas { get; set; }
        public int NotasNuevas { get; set; }
        public int NotasYaExistentes { get; set; }
        public int ConflictosDeNotas { get; set; }
        public int NotasInvalidas { get; set; }
        public int PendientesDeRevision { get; set; }
    }

    public class ImportacionIssueDto
    {
        public string Codigo { get; set; } = null!;
        public string Severidad { get; set; } = null!;
        public string Mensaje { get; set; } = null!;
        public string? SlotKey { get; set; }
    }

    public class ImportacionStudentOptionDto
    {
        public Guid IdEstudiante { get; set; }
        public string Label { get; set; } = null!;
        public string Documento { get; set; } = null!;
    }

    public class ImportacionSlotDto
    {
        public string SlotKey { get; set; } = null!;
        public Guid? IdIE { get; set; }
        public int EvaluacionNumero { get; set; }
        public string TipoCalificacion { get; set; } = null!;
        public string Label { get; set; } = null!;
        public bool TieneNotasImportadas { get; set; }
        public bool TieneEstructuraPrevia { get; set; }
        public bool AdmiteCargaNotas { get; set; }
    }

    public class ImportacionRevisionRowDto
    {
        public string RowId { get; set; } = null!;
        public int Orden { get; set; }
        public string EstudiantePdf { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public string? Mensaje { get; set; }
        public Guid? EstudianteAsociadoId { get; set; }
        public bool RequiereAsociacionManual { get; set; }
        public List<Guid> CandidatosEstudianteIds { get; set; } = new();
        public List<ImportacionIssueDto> Issues { get; set; } = new();
        public List<ImportacionRevisionCellDto> Cells { get; set; } = new();
    }

    public class ImportacionRevisionCellDto
    {
        public string SlotKey { get; set; } = null!;
        public Guid? IdCalificacionBase { get; set; }
        public int EvaluacionNumero { get; set; }
        public string TipoCalificacion { get; set; } = null!;
        public string? ValorImportadoRaw { get; set; }
        public int? ValorImportado { get; set; }
        public int? ValorDb { get; set; }
        public int? ValorFinal { get; set; }
        public string Estado { get; set; } = null!;
        public string Resolucion { get; set; } = null!;
        public string? Mensaje { get; set; }
    }

    public class ImportacionConfirmacionResumenDto
    {
        public int EstudiantesValidados { get; set; }
        public int NotasNuevas { get; set; }
        public int NotasExistentesMantenidas { get; set; }
        public int NotasReemplazadas { get; set; }
        public int NotasQuitadas { get; set; }
    }

    public class ConfirmarImportacionCalificacionesResponseDto
    {
        public Guid IdImportacionCalificaciones { get; set; }
        public string Estado { get; set; } = null!;
        public string? RutaArchivoFinal { get; set; }
        public int CambiosAplicados { get; set; }
        public Guid? IdSesionAuditoria { get; set; }
    }
}
