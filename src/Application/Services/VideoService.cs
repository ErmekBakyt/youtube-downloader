using YoutubeDownloader.Application.DTOs;
using YoutubeDownloader.Application.Interfaces;
using YoutubeDownloader.Domain.Entities;

namespace YoutubeDownloader.Application.Services;

public class VideoService
{
    private readonly IYoutubeService _youtubeService;

    public VideoService(IYoutubeService youtubeService)
    {
        _youtubeService = youtubeService;
    }

    public bool IsValidUrl(string url) => _youtubeService.IsValidYoutubeUrl(url);

    public async Task<ApiResponseDto<VideoInfoDto>> GetVideoInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return ApiResponseDto<VideoInfoDto>.Fail("URL boş olamaz.");

            if (!_youtubeService.IsValidYoutubeUrl(url))
                return ApiResponseDto<VideoInfoDto>.Fail("Geçerli bir YouTube URL'si giriniz.");

            var videoInfo = await _youtubeService.GetVideoInfoAsync(url, cancellationToken);
            var dto = MapToDto(videoInfo);
            return ApiResponseDto<VideoInfoDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<VideoInfoDto>.Fail($"Video bilgisi alınamadı: {ex.Message}");
        }
    }

    public async Task<(Stream? Stream, string FileName, string ContentType)> DownloadVideoAsync(
        string url, string formatId, CancellationToken cancellationToken = default)
    {
        if (!_youtubeService.IsValidYoutubeUrl(url))
            throw new ArgumentException("Geçersiz YouTube URL'si.");

        var videoInfo = await _youtubeService.GetVideoInfoAsync(url, cancellationToken);
        var format = videoInfo.AvailableFormats.FirstOrDefault(f => f.FormatId == formatId);

        // download
        var stream = await _youtubeService.DownloadVideoAsync(url, formatId, cancellationToken);

        // file ext + content-type
        var ext = (format?.Extension ?? "mp4").ToLowerInvariant();

        // safe filename
        var safeTitle = string.Concat(videoInfo.Title.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "video";

        var fileName = $"{safeTitle}.{ext}";

        var contentType = ext switch
        {
            "mp4" => "video/mp4",
            "webm" => "video/webm",
            "m4a" => "audio/mp4",
            _ => ext == "mp3" ? "audio/mpeg" : "application/octet-stream"
        };

        return (stream, fileName, contentType);
    }

    private VideoInfoDto MapToDto(VideoInfo videoInfo)
    {
        // ✅ IMPORTANT: audio-only да чыгыш керек, “mp3” деп текшербейбиз
        var formats = videoInfo.AvailableFormats
            .OrderByDescending(f => f.IsRecommended)               // recommended биринчи
            .ThenByDescending(f => ParseHeightSafe(f.Resolution))  // анан резолюция
            .ThenByDescending(f => f.Bitrate)                      // анан bitrate
            .Select(f => new VideoFormatDto
            {
                FormatId = f.FormatId,
                Quality = f.Quality,
                Resolution = f.Resolution,
                Extension = f.Extension,
                FileSize = f.FileSize,
                Label = BuildLabel(f),
                IsRecommended = f.IsRecommended                     // ✅ i==0 ЭМЕС
            })
            .ToList();

        return new VideoInfoDto
        {
            VideoId = videoInfo.VideoId,
            Title = videoInfo.Title,
            Author = videoInfo.Author,
            ThumbnailUrl = videoInfo.ThumbnailUrl,
            Duration = FormatDuration(videoInfo.Duration),
            ViewCount = FormatViewCount(videoInfo.ViewCount),
            Formats = formats
        };
    }

    private static string BuildLabel(VideoFormat f)
    {
        // Audio-only
        if (!f.HasVideo)
            return $"🎵 Audio ({f.Extension.ToUpper()}) - {f.Quality}";

        // Muxed (video+audio)
        if (f.HasAudio)
            return $"🎬 {f.Resolution} {f.Extension.ToUpper()} (Video+Audio)";

        // Video-only (HD)
        return $"🎞️ {f.Resolution} {f.Extension.ToUpper()} (HD, ses ayrı)";
    }

    private static int ParseHeightSafe(string resolution)
    {
        var digits = new string((resolution ?? "").Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var h) ? h : 0;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.Hours > 0)
            return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    private static string FormatViewCount(long count)
    {
        return count switch
        {
            >= 1_000_000_000 => $"{count / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{count / 1_000_000.0:F1}M",
            >= 1_000 => $"{count / 1000.0:F1}K",
            _ => count.ToString()
        };
    }
}
