namespace TesisGestorApi.DTOs
{
    public class EmailInlineResourceDto
    {
        public string ContentId { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
