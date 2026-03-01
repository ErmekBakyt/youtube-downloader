namespace Application.DTOs;

public class VideoInfoDto
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string ViewCount { get; set; } = string.Empty;
    public List<VideoFormatDto> Formats { get; set; } = new();
}

public class VideoFormatDto
{
    public string FormatId { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsRecommended { get; set; }
}

public class DownloadRequestDto
{
    public string Url { get; set; } = string.Empty;
    public string FormatId { get; set; } = string.Empty;
}

public class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }

    public static ApiResponseDto<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponseDto<T> Fail(string error) => new() { Success = false, ErrorMessage = error };
}
