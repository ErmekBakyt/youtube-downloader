using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

public class HomeController(VideoService videoService, ILogger<HomeController> logger) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetVideoInfo([FromBody] GetVideoInfoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Url))
            return Json(ApiResponseDto<VideoInfoDto>.Fail("URL giriniz."));

        var result = await videoService.GetVideoInfoAsync(request.Url, cancellationToken);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download([FromBody] DownloadRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Url) || string.IsNullOrWhiteSpace(request.FormatId))
                return BadRequest("Geçersiz istek.");

            var (stream, fileName, contentType) = await videoService.DownloadVideoAsync(
                request.Url, request.FormatId, cancellationToken);

            if (stream == null)
                return BadRequest("İndirme başarısız.");

            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Download failed for URL: {Url}", request?.Url);
            return StatusCode(500, "İndirme sırasında bir hata oluştu.");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}

public class GetVideoInfoRequest
{
    public string Url { get; set; } = string.Empty;
}
