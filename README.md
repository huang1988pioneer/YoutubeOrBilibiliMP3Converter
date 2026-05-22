# YouTube to MP3 Converter

Avalonia GUI app that converts up to three YouTube URLs to MP3 using `yt-dlp` and `ffmpeg`.

## Run

```bash
dotnet run
```

## Windows setup

Install the required command-line tools:

```powershell
winget install yt-dlp.yt-dlp Gyan.FFmpeg
```

Restart the terminal after installing so `yt-dlp.exe` and `ffmpeg.exe` are available from `PATH`.

## Build a Windows EXE

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1
```

The default output is `publish\win-x64\YoutubeToMP3Converter.exe`.

For Windows on ARM:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1 -Runtime win-arm64
```

## macOS setup

```bash
brew install yt-dlp ffmpeg
```

## Notes

Converted MP3 files are saved to the selected output folder. The last valid folder is remembered in the user's application data directory.
