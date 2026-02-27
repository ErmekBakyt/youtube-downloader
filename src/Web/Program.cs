using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;

using YoutubeDownloader.Application.Interfaces;
using YoutubeDownloader.Application.Services;
using YoutubeDownloader.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ✅ Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ✅ MVC + View localization
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix) // opsiyonel: Index.ru.cshtml gibi
    .AddDataAnnotationsLocalization();

// Clean Architecture DI Registration
builder.Services.AddScoped<IYoutubeService, YoutubeService>();
builder.Services.AddScoped<VideoService>();

// Response caching & compression
builder.Services.AddResponseCaching();
builder.Services.AddResponseCompression();

var app = builder.Build();

// ✅ Supported cultures (5 dil)
var supportedCultures = new[]
{
    new CultureInfo("ky"), // Kyrgyz
    new CultureInfo("kk"), // Kazakh
    new CultureInfo("ru"), // Russian
    new CultureInfo("en"), // English
    new CultureInfo("de"), // German (yaygın Avrupa dili)
};

// ✅ Request localization middleware (cookie + querystring + header)
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseResponseCaching();
app.UseResponseCompression();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();