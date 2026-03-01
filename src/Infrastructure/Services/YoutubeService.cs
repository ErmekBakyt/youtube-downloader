using System.Diagnostics;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Entities;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace Infrastructure.Services;

public partial class YoutubeService : IYoutubeService
{
    private readonly YoutubeClient _youtubeClient;

    public YoutubeService(HttpClient http)
    {
        // YoutubeExplode ушул HttpClient аркылуу чыгат
        _youtubeClient = new YoutubeClient(http);
    }

    public bool IsValidYoutubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return YoutubeUrlRegex().IsMatch(url);
    }

    public async Task<VideoInfo> GetVideoInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        var video = await _youtubeClient.Videos.GetAsync(url, cancellationToken);
        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url, cancellationToken);

        var formats = new List<VideoFormat>();

        // 1) Muxed (video+audio) - simple downloads
        foreach (var s in manifest.GetMuxedStreams()
                     .OrderByDescending(x => x.VideoQuality.MaxHeight))
        {
            formats.Add(new VideoFormat
            {
                FormatId = BuildMuxedId(s),
                Quality = s.VideoQuality.Label,
                Resolution = $"{s.VideoQuality.MaxHeight}p",
                Extension = s.Container.Name, // mp4/webm
                FileSize = FormatFileSize(s.Size.Bytes),
                HasAudio = true,
                HasVideo = true,
                Bitrate = (int)Math.Round(s.Bitrate.KiloBitsPerSecond),
                MimeType = GetMimeType(s.Container.Name, isAudio: false),
                IsRecommended = false
            });
        }

        // 2) Video-only (HD) - prefer MP4 for safe merge into mp4
        foreach (var s in manifest.GetVideoOnlyStreams()
                     .OrderByDescending(x => x.VideoQuality.MaxHeight)
                     .ThenByDescending(x => x.Bitrate))
        {
            formats.Add(new VideoFormat
            {
                FormatId = BuildVideoOnlyId(s),
                Quality = s.VideoQuality.Label,
                Resolution = $"{s.VideoQuality.MaxHeight}p",
                Extension = s.Container.Name, // mp4/webm
                FileSize = FormatFileSize(s.Size.Bytes),
                HasAudio = false,
                HasVideo = true,
                Bitrate = (int)Math.Round(s.Bitrate.KiloBitsPerSecond),
                MimeType = GetMimeType(s.Container.Name, isAudio: false),
                IsRecommended = false
            });
        }

        // 3) Audio-only best (merge үчүн керек)
        var bestAudio = manifest.GetAudioOnlyStreams()
            .OrderByDescending(x => x.Bitrate)
            .FirstOrDefault();

        if (bestAudio != null)
        {
            formats.Add(new VideoFormat
            {
                FormatId = BuildAudioOnlyId(bestAudio),
                Quality = $"{bestAudio.Bitrate.KiloBitsPerSecond:F0} kbps",
                Resolution = "Audio Only",
                Extension = bestAudio.Container.Name, // mp4(webm) (чыныгы контейнер!)
                FileSize = FormatFileSize(bestAudio.Size.Bytes),
                HasAudio = true,
                HasVideo = false,
                Bitrate = (int)Math.Round(bestAudio.Bitrate.KiloBitsPerSecond),
                MimeType = GetMimeType(bestAudio.Container.Name, isAudio: true),
                IsRecommended = false
            });
        }

        // Recommended: эң жогорку VIDEO-ONLY MP4 болсо ошону, болбосо эң жогорку MUXED
        var recommended =
            formats.Where(f => f.FormatId.StartsWith("video|") && f.Extension == "mp4")
                   .OrderByDescending(f => ParseHeight(f.Resolution))
                   .ThenByDescending(f => f.Bitrate)
                   .FirstOrDefault()
            ?? formats.Where(f => f.FormatId.StartsWith("video|"))
                      .OrderByDescending(f => ParseHeight(f.Resolution))
                      .ThenByDescending(f => f.Bitrate)
                      .FirstOrDefault()
            ?? formats.Where(f => f.FormatId.StartsWith("muxed|"))
                      .OrderByDescending(f => ParseHeight(f.Resolution))
                      .ThenByDescending(f => f.Bitrate)
                      .FirstOrDefault();

        if (recommended != null)
            recommended.IsRecommended = true;

        return new VideoInfo
        {
            VideoId = video.Id.Value,
            Title = video.Title,
            Author = video.Author.ChannelTitle,
            ThumbnailUrl = video.Thumbnails.GetWithHighestResolution()?.Url ?? "",
            Duration = video.Duration ?? TimeSpan.Zero,
            ViewCount = video.Engagement.ViewCount,
            AvailableFormats = formats
        };
    }

    // ===== DOWNLOAD =====

    public async Task<Stream> DownloadVideoAsync(string url, string formatId, CancellationToken cancellationToken = default)
    {
        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(url, cancellationToken);

        if (formatId.StartsWith("muxed|"))
        {
            var muxed = manifest.GetMuxedStreams()
                .FirstOrDefault(s => BuildMuxedId(s) == formatId)
                ?? throw new InvalidOperationException("Muxed stream not found");

            return await CopyToMemory(muxed, cancellationToken);
        }

        if (formatId.StartsWith("audio|"))
        {
            var audio = manifest.GetAudioOnlyStreams()
                .FirstOrDefault(s => BuildAudioOnlyId(s) == formatId)
                ?? throw new InvalidOperationException("Audio stream not found");

            return await CopyToMemory(audio, cancellationToken);
        }

        if (formatId.StartsWith("video|"))
        {
            // 1) exactly selected video-only
            var selectedVideo = manifest.GetVideoOnlyStreams()
                .FirstOrDefault(s => BuildVideoOnlyId(s) == formatId)
                ?? throw new InvalidOperationException("Video stream not found");

            // 2) choose best audio compatible
            // Prefer mp4 audio for mp4 output, else use best available
            var audio = manifest.GetAudioOnlyStreams()
                .Where(a => selectedVideo.Container.Name == "mp4" ? a.Container.Name == "mp4" : true)
                .OrderByDescending(a => a.Bitrate)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("No audio stream found");

            // 3) merge
            // If video is mp4 -> output mp4; if webm -> output webm
            var outExt = selectedVideo.Container.Name == "webm" ? "webm" : "mp4";
            return await MergeVideoAudioWithFfmpeg(selectedVideo, audio, outExt, cancellationToken);
        }

        throw new InvalidOperationException("Invalid formatId");
    }

    // ===== Fingerprint IDs (Itag/Tag жок) =====

    private static string BuildMuxedId(MuxedStreamInfo s)
        => $"muxed|{s.Container.Name}|{s.VideoQuality.Label}|{Math.Round(s.Bitrate.KiloBitsPerSecond)}";

    private static string BuildVideoOnlyId(IVideoStreamInfo s)
        => $"video|{s.Container.Name}|{s.VideoQuality.Label}|{s.VideoCodec}|{Math.Round(s.Bitrate.KiloBitsPerSecond)}";

    private static string BuildAudioOnlyId(IAudioStreamInfo s)
        => $"audio|{s.Container.Name}|{Math.Round(s.Bitrate.KiloBitsPerSecond)}";

    private static int ParseHeight(string resolution)
    {
        var digits = new string((resolution ?? "").Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var h) ? h : 0;
    }

    // ===== Helpers =====

    private async Task<Stream> CopyToMemory(IStreamInfo info, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await _youtubeClient.Videos.Streams.CopyToAsync(info, ms, cancellationToken: ct);
        ms.Position = 0;
        return ms;
    }

    private async Task<Stream> MergeVideoAudioWithFfmpeg(
        IVideoStreamInfo video,
        IAudioStreamInfo audio,
        string outExt,
        CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "yt-downloader");
        Directory.CreateDirectory(tempDir);

        var videoPath = Path.Combine(tempDir, $"{Guid.NewGuid()}.video.{video.Container.Name}");
        var audioPath = Path.Combine(tempDir, $"{Guid.NewGuid()}.audio.{audio.Container.Name}");
        var outPath = Path.Combine(tempDir, $"{Guid.NewGuid()}.{outExt}");

        try
        {
            await using (var vfs = File.Create(videoPath))
                await _youtubeClient.Videos.Streams.CopyToAsync(video, vfs, cancellationToken: ct);

            await using (var afs = File.Create(audioPath))
                await _youtubeClient.Videos.Streams.CopyToAsync(audio, afs, cancellationToken: ct);

            // If containers mismatch badly, ffmpeg will complain; stderr will show.
            await RunFfmpegAsync(
                $"-y -i \"{videoPath}\" -i \"{audioPath}\" -c copy -movflags +faststart \"{outPath}\"",
                ct);

            var bytes = await File.ReadAllBytesAsync(outPath, ct);
            return new MemoryStream(bytes);
        }
        finally
        {
            SafeDelete(videoPath);
            SafeDelete(audioPath);
            SafeDelete(outPath);
        }
    }

    private static async Task RunFfmpegAsync(string args, CancellationToken ct)
    {
        // Rider/PATH issues -> fallback
        var ffmpegPath =
            Environment.GetEnvironmentVariable("FFMPEG_PATH")
            ?? (File.Exists(@"C:\ffmpeg\bin\ffmpeg.exe") ? @"C:\ffmpeg\bin\ffmpeg.exe" : "ffmpeg");

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"ffmpeg başlatılamadı. Path: {ffmpegPath}");

        var stderrTask = p.StandardError.ReadToEndAsync();
        var stdoutTask = p.StandardOutput.ReadToEndAsync();

        await p.WaitForExitAsync(ct);

        var stderr = await stderrTask;
        var stdout = await stdoutTask;

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg hata (exit={p.ExitCode}): {stderr}\n{stdout}");
    }

    private static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static string GetMimeType(string ext, bool isAudio)
    {
        ext = (ext ?? "").ToLowerInvariant();

        if (isAudio)
        {
            return ext switch
            {
                "mp4" => "audio/mp4",   // m4a
                "webm" => "audio/webm",
                _ => "application/octet-stream"
            };
        }

        return ext switch
        {
            "mp4" => "video/mp4",
            "webm" => "video/webm",
            _ => "application/octet-stream"
        };
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }

    [GeneratedRegex(@"^(https?://)?(www\.)?(youtube\.com|youtu\.be)/.+$", RegexOptions.IgnoreCase)]
    private static partial Regex YoutubeUrlRegex();
}
