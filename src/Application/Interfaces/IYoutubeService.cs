using YoutubeDownloader.Domain.Entities;

namespace YoutubeDownloader.Application.Interfaces;

public interface IYoutubeService
{
    Task<VideoInfo> GetVideoInfoAsync(string url, CancellationToken cancellationToken = default);
    Task<Stream> DownloadVideoAsync(string url, string formatId, CancellationToken cancellationToken = default);
    bool IsValidYoutubeUrl(string url);
}
