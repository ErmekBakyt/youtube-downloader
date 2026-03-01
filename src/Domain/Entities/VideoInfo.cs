namespace Domain.Entities;

public class VideoInfo
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long ViewCount { get; set; }
    public List<VideoFormat> AvailableFormats { get; set; } = new();
}

public class VideoFormat
{
    public string FormatId { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
    public bool HasVideo { get; set; }
    public int Bitrate { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public bool IsRecommended { get; set; } 
}
