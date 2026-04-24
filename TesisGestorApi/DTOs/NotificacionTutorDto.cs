namespace TesisGestorApi.DTOs
{
    /// <summary>
    /// Respuesta del endpoint que envía una notificación por mail al tutor principal
    /// cuando sus datos llevan más de 6 meses sin actualizarse.
    /// </summary>
    public class NotificacionTutorResponseDto
    {
        /// <summary>Indica si el mail fue enviado exitosamente.</summary>
        public bool Enviado { get; set; }

        /// <summary>Mensaje descriptivo del resultado (éxito o motivo del rechazo).</summary>
        public string Mensaje { get; set; } = string.Empty;
    }
}
