namespace DbIpToSql
{
    internal sealed class DownloadUrlResponse
    {
        public required string DownloadUrl { get; init; }
        public required string Filename { get; init; }
        public required DateTime DataVersion { get; init; }
    }
}
