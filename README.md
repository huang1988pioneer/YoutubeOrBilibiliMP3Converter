# YouTube to MP3 Converter

Avalonia 桌面 GUI 版本，可一次貼上最多三個 YouTube 影片或播放清單連結，透過 `yt-dlp` 與 `ffmpeg` 依序轉成 MP3。

## 執行

```bash
dotnet run
```

## 必要工具

macOS 建議使用 Homebrew 安裝：

```bash
brew install yt-dlp ffmpeg
```

轉檔時，MP3 會輸出到畫面中選擇的資料夾。輸出資料夾可以直接修改或用按鈕選擇，程式會記住上一次使用的有效資料夾，下一次開啟時自動帶入。
