# TAYN DM

A modern Windows download manager with Persian and English interfaces.

**Designed and developed by [thearyan](https://github.com/TaynDL).**

## Features

- Segmented downloads with automatic fallback
- Pause and resume support
- Download queue and priority management
- Speed limits, proxy settings and concurrent downloads
- Search, filters and detailed progress information
- SHA-256 calculation and optional Windows Defender scan
- Persian and English user interfaces
- Portable and installable Windows editions
- **NEW v3.0: Revolutionary Aurora UI (Dark/Light themes)**
- **NEW v3.0: Animated progress bars with live speed charts**
- **NEW v3.0: MTProto proxy auto-discovery for Iran**
- **NEW v3.0: Responsive layout with collapsible panels**

## Requirements

- Windows 10 or Windows 11
- Microsoft .NET 8 Desktop Runtime (for portable version)

## Build

```powershell
dotnet build -c Release
dotnet run --project tests/DownloadYar.Tests.csproj -c Release
```

## Usage

1. Select **New Download**.
2. Paste a direct file URL.
3. Choose the destination folder.
4. Start the download.
5. Use **Pause** and **Start** to pause or resume.

The supplied URL must point directly to a file. A regular webpage or video-player page is not necessarily downloadable as a media file.

## Releases

Windows Setup and Portable packages are published in GitHub Releases. Pushing a tag such as `v3.0.0` automatically runs tests, builds both packages and creates a Release.

## License

Copyright © 2026 **thearyan**. All rights reserved. See [LICENSE](LICENSE).