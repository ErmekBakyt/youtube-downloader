# 🎬 YTDownloader — YouTube Downloader

Modern, dark-themed YouTube downloader built with **ASP.NET Core 10 MVC** using **Clean Architecture**.

## 📁 Project Structure (Clean Architecture)

```
YoutubeDownloader/
├── src/
│   ├── Core/
│   │   ├── YoutubeDownloader.Domain/           # Entities, Value Objects
│   │   │   └── Entities/
│   │   │       ├── VideoInfo.cs
│   │   │       └── DownloadRequest.cs
│   │   │
│   │   └── YoutubeDownloader.Application/      # Business Logic
│   │       ├── DTOs/
│   │       │   └── VideoInfoDto.cs
│   │       ├── Interfaces/
│   │       │   └── IYoutubeService.cs
│   │       └── Services/
│   │           └── VideoService.cs
│   │
│   ├── Infrastructure/
│   │   └── YoutubeDownloader.Infrastructure/   # External Integrations
│   │       └── Services/
│   │           └── YoutubeService.cs           # Uses YoutubeExplode
│   │
│   └── Web/
│       └── YoutubeDownloader.Web/              # ASP.NET Core MVC
│           ├── Controllers/
│           │   └── HomeController.cs
│           ├── Views/
│           │   ├── Home/
│           │   │   └── Index.cshtml
│           │   └── Shared/
│           │       └── _Layout.cshtml
│           └── wwwroot/
│               ├── css/site.css
│               └── js/downloader.js
│
└── YoutubeDownloader.sln
```

## 🚀 Kurulum & Çalıştırma

### Gereksinimler
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Adımlar

```bash
# Repoyu klonlayın veya dosyaları indirin

# Solution klasörüne gidin
cd YoutubeDownloader

# Bağımlılıkları yükleyin
dotnet restore

# Web projesini çalıştırın
cd src/Web/YoutubeDownloader.Web
dotnet run
```

Uygulama `https://localhost:5001` adresinde açılacaktır.

## ✨ Özellikler

- 🎬 **Çoklu kalite seçeneği** — 4K, 1080p, 720p, 480p, 360p
- 🎵 **MP3 ses indirme** — Yalnızca ses formatı
- 🖼️ **Video önizleme** — Thumbnail, başlık, kanal, görüntülenme
- ⚡ **Otomatik yapıştır** — YouTube linki yapıştırınca otomatik analiz
- 🌙 **Dark Mode UI** — Modern glassmorphism tasarım
- 🔒 **CSRF koruması** — AntiForgery token

## 📦 Kullanılan Paketler

| Paket | Amaç |
|-------|------|
| [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) | YouTube video bilgisi ve stream indirme |
| ASP.NET Core 10 MVC | Web framework |

## 🏗️ Mimari

**Clean Architecture** prensiplerine uygun:

- **Domain** → Hiçbir dışa bağımlılık yok. Saf entity'ler.
- **Application** → Domain'e bağımlı. Interface'ler ve iş mantığı.
- **Infrastructure** → Application interface'lerini implement eder. YoutubeExplode kullanır.
- **Web** → Application service'lerini kullanır. Controller → Service → Infrastructure.

## ⚠️ Yasal Uyarı

Bu araç yalnızca telif hakkı koruması olmayan veya izinli içerikler için kullanılmalıdır.
YouTube Hizmet Şartları'na uygun kullanım kullanıcının sorumluluğundadır.
