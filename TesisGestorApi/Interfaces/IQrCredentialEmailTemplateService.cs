namespace TesisGestorApi.Interfaces
{
    public interface IQrCredentialEmailTemplateService
    {
        string Build(
            string tutorNombre,
            string alumnoNombre,
            int anioLectivo,
            Guid codigoQr,
            string? mensajePersonalizado,
            string qrInlineContentId);
    }
}
