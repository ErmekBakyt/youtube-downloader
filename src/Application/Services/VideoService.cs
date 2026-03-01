using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class VideoService(
    IYoutubeService youtubeService,
    IStringLocalizer<VideoService> L,
    ILogger<VideoService> logger)
{
    public bool IsValidUrl(string url) => youtubeService.IsValidYoutubeUrl(url);

    public async Task<ApiResponseDto<VideoInfoDto>> GetVideoInfoAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return ApiResponseDto<VideoInfoDto>.Fail(L["UrlEmpty"]);

            if (!youtubeService.IsValidYoutubeUrl(url))
                return ApiResponseDto<VideoInfoDto>.Fail(L["InvalidYoutubeUrl"]);

            var videoInfo = await youtubeService.GetVideoInfoAsync(url, cancellationToken);
            var dto = MapToDto(videoInfo);
            return ApiResponseDto<VideoInfoDto>.Ok(dto);
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(oce, "GetVideoInfoAsync canceled. Url={Url}", url);
            return ApiResponseDto<VideoInfoDto>.Fail(L["VideoInfoFailed"]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetVideoInfoAsync failed. Url={Url}", url);
            return ApiResponseDto<VideoInfoDto>.Fail(L["VideoInfoFailed"]);
        }
    }

    public async Task<(Stream? Stream, string FileName, string ContentType)> DownloadVideoAsync(
        string url,
        string formatId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!youtubeService.IsValidYoutubeUrl(url))
                throw new ArgumentException(L["InvalidYoutubeUrlArg"]);

            var videoInfo = await youtubeService.GetVideoInfoAsync(url, cancellationToken);

            var format = videoInfo.AvailableFormats.FirstOrDefault(f => f.FormatId == formatId);
            if (format is null)
                throw new InvalidOperationException(L["FormatNotFound"]);

            var stream = await youtubeService.DownloadVideoAsync(url, formatId, cancellationToken);

            var ext = (format.Extension ?? "mp4").ToLowerInvariant();

            var safeTitle = string.Concat((videoInfo.Title ?? "video").Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "video";

            var fileName = $"{safeTitle}.{ext}";

            var contentType = ext switch
            {
                "mp4" => "video/mp4",
                "webm" => "video/webm",
                "m4a" => "audio/mp4",
                "mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };

            return (stream, fileName, contentType);
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(oce, "DownloadVideoAsync canceled. Url={Url}, FormatId={FormatId}", url, formatId);
            throw; // controller 499/400’a çevirebilir
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DownloadVideoAsync failed. Url={Url}, FormatId={FormatId}", url, formatId);
            // Controller tarafında bunu yakalayıp L["DownloadFailed"] dönebilirsin,
            // ama burada da exception mesajını "safe" hale getiriyoruz:
            throw new InvalidOperationException(L["DownloadFailed"]);
        }
    }

    private VideoInfoDto MapToDto(VideoInfo videoInfo)
    {
        var formats = videoInfo.AvailableFormats
            .OrderByDescending(f => f.IsRecommended)
            .ThenByDescending(f => ParseHeightSafe(f.Resolution))
            .ThenByDescending(f => f.Bitrate)
            .Select(f => new VideoFormatDto
            {
                FormatId = f.FormatId,
                Quality = f.Quality,
                Resolution = f.Resolution,
                Extension = f.Extension,
                FileSize = f.FileSize,
                Label = BuildLabel(f),
                IsRecommended = f.IsRecommended
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
        if (!f.HasVideo)
            return $"🎵 Audio ({f.Extension.ToUpper()}) - {f.Quality}";

        if (f.HasAudio)
            return $"🎬 {f.Resolution} {f.Extension.ToUpper()} (Video+Audio)";

        return $"🎞️ {f.Resolution} {f.Extension.ToUpper()} (HD, separate audio)";
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