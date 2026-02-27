using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace YoutubeDownloader.Web.Controllers;

public class CultureController : Controller
{
    [HttpGet("/culture/set")]
    public IActionResult Set(string culture, string returnUrl = "/")
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );

        return LocalRedirect(returnUrl);
    }
}