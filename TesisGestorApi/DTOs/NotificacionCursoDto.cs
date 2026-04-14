namespace TesisGestorApi.DTOs
{
    /// <summary>
    /// Respuesta del endpoint que envía notificaciones masivas a los tutores
    /// principales de un curso cuyos datos llevan más de 6 meses sin actualizarse.
    /// </summary>
    public class NotificacionCursoResponseDto
    {
        /// <summary>Cantidad de mails enviados exitosamente.</summary>
        public int Enviados { get; set; }

        /// <summary>
        /// Cantidad de estudiantes omitidos (sin tutor principal o con datos recientes).
        /// </summary>
        public int Omitidos { get; set; }

        /// <summary>Mensaje descriptivo del resultado.</summary>
        public string Mensaje { get; set; } = string.Empty;
    }
}
