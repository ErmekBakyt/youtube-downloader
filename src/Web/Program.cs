using System.Globalization;
using Application.Interfaces;
using Application.Services;
using Infrastructure.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ✅ Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ✅ MVC + View localization
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

// ✅ Antiforgery (MUST be before Build)
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
});

// ✅ Forwarded headers options (MUST be before Build)
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Clean Architecture DI Registration
builder.Services.AddScoped<IYoutubeService, YoutubeService>();
builder.Services.AddScoped<VideoService>();

// Response caching & compression
builder.Services.AddResponseCaching();
builder.Services.AddResponseCompression();

var app = builder.Build();

// ✅ ForwardedHeaders should be early (before HSTS/HTTPS redirection)
app.UseForwardedHeaders();

// ✅ Supported cultures (5 dil)
var supportedCultures = new[]
{
    new CultureInfo("ky"),
    new CultureInfo("kk"),
    new CultureInfo("ru"),
    new CultureInfo("en"),
    new CultureInfo("de"),
};

// ✅ Request localization middleware
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // HSTS only in prod, good
}

// ⚠️ nginx TLS termination болсо, app.UseHttpsRedirection() көбүнчө кереги жок.
// Эгер калтырсаң, ForwardedHeaders жогору турганы үчүн туура иштейт.
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.UseResponseCaching();
app.UseResponseCompression();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();