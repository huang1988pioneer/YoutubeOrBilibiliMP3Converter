# YouTube to MP3 Converter

Avalonia GUI app that converts up to three YouTube URLs to MP3 using `yt-dlp` and `ffmpeg`.

## Run

```bash
dotnet run
```

## Windows 使用前安裝

第一次使用前，請先安裝轉檔需要的兩個小工具。只要照下面做一次就好。

1. 按鍵盤的 `Windows` 鍵。
2. 輸入 `終端機` 或 `Terminal`。
3. 在「終端機」上按右鍵，選擇「以系統管理員身分執行」。
4. 複製下面這行指令，貼到終端機裡，然後按 Enter：

```powershell
winget install yt-dlp.yt-dlp Gyan.FFmpeg
```

5. 如果畫面問你是否同意，輸入 `Y`，再按 Enter。
6. 等安裝完成後，關掉終端機。
7. 重新開啟 YouTube to MP3 Converter，就可以開始使用。

如果 Windows 顯示找不到 `winget`，請先從 Microsoft Store 更新或安裝「應用程式安裝程式」。

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
