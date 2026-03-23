namespace TesisGestorApi.Interfaces
{
    public interface IQrCredentialVisualService
    {
        byte[] BuildQrPng(Guid code, int pixelsPerModule = 20);
    }
}
