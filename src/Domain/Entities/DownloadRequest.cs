namespace YoutubeDownloader.Domain.Entities;

public class DownloadRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string FormatId { get; set; } = string.Empty;
    public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum DownloadStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
