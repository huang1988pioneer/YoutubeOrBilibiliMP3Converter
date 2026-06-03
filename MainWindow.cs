using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace YoutubeOrBilibiliMP3Converter;

public sealed class MainWindow : Window
{
    private static readonly int[] UrlInputCountOptions = [1, 3, 7];
    private static readonly string[] OutputFormatOptions = ["MP3", "MP4"];
    private static readonly string[] Mp4QualityOptions = ["1080p", "4K"];
    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding SystemAnsiEncoding = GetSystemAnsiEncoding();

    private readonly TextBox[] _urlBoxes;
    private readonly ComboBox _urlInputCountComboBox;
    private readonly ComboBox _outputFormatComboBox;
    private readonly ComboBox _mp4QualityComboBox;
    private readonly TextBox _outputBox;
    private readonly Button _chooseFolderButton;
    private readonly Button _clearUrlsButton;
    private readonly Button _convertButton;
    private readonly TextBlock _statusText;
    private readonly TextBox _logBox;
    private readonly ProgressBar _progressBar;

    private int _urlInputCount;
    private string _outputFormat;
    private string _mp4Quality;
    private CancellationTokenSource? _conversionTokenSource;

    public MainWindow()
    {
        var settings = AppSettings.Load();

        Title = "YouTube / Bilibili to MP3 Converter";
        Width = 820;
        Height = 620;
        MinWidth = 680;
        MinHeight = 520;
        Background = Brush.Parse("#F6F7F9");
        _urlInputCount = settings.UrlInputCount;
        _outputFormat = settings.OutputFormat;
        _mp4Quality = settings.Mp4Quality;

        _urlBoxes = Enumerable.Range(1, UrlInputCountOptions.Max())
            .Select(index => CreateUrlBox($"網址 {index}"))
            .ToArray();

        _urlInputCountComboBox = new ComboBox
        {
            ItemsSource = UrlInputCountOptions,
            SelectedItem = _urlInputCount,
            MinWidth = 96,
            MinHeight = 34
        };
        _urlInputCountComboBox.SelectionChanged += UrlInputCountChanged;

        _outputFormatComboBox = new ComboBox
        {
            ItemsSource = OutputFormatOptions,
            SelectedItem = _outputFormat,
            MinWidth = 96,
            MinHeight = 34
        };
        _outputFormatComboBox.SelectionChanged += OutputFormatChanged;

        _mp4QualityComboBox = new ComboBox
        {
            ItemsSource = Mp4QualityOptions,
            SelectedItem = _mp4Quality,
            MinWidth = 96,
            MinHeight = 34
        };
        _mp4QualityComboBox.SelectionChanged += Mp4QualityChanged;

        _outputBox = new TextBox
        {
            Text = settings.LastOutputFolder,
            IsReadOnly = false,
            FontSize = 14,
            MinHeight = 38
        };
        _outputBox.LostFocus += (_, _) => SaveOutputFolderIfValid();

        _chooseFolderButton = new Button
        {
            Content = "選擇資料夾",
            MinWidth = 112,
            MinHeight = 38
        };
        _chooseFolderButton.Click += ChooseFolderAsync;

        _clearUrlsButton = new Button
        {
            Content = "清除網址",
            MinWidth = 112,
            MinHeight = 42,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _clearUrlsButton.Click += ClearUrls;

        _convertButton = new Button
        {
            Content = "轉換",
            MinWidth = 128,
            MinHeight = 42,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _convertButton.Click += ConvertOrCancelAsync;

        _statusText = new TextBlock
        {
            Text = "準備就緒",
            FontSize = 14,
            Foreground = Brush.Parse("#394150"),
            VerticalAlignment = VerticalAlignment.Center
        };

        _progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Height = 6,
            Minimum = 0,
            Maximum = 100
        };

        _logBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas, Microsoft JhengHei UI, monospace"),
            FontSize = 12,
            Background = Brush.Parse("#111827"),
            Foreground = Brush.Parse("#F9FAFB"),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14),
            MinHeight = 210
        };

        Content = BuildLayout();
        UpdateUrlBoxVisibility();
        UpdateMp4QualityVisibility();
        UpdateConvertButtonText();
        Opened += (_, _) => CheckTools();
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(28)
        };

        var header = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 22)
        };
        header.Children.Add(new TextBlock
        {
            Text = "YouTube / Bilibili to MP3 Converter",
            FontSize = 28,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#111827")
        });
        header.Children.Add(new TextBlock
        {
            Text = "貼上 YouTube 或 Bilibili 連結，選擇輸出資料夾後轉換成 MP3 或 MP4。",
            FontSize = 14,
            Foreground = Brush.Parse("#5F6877")
        });

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*"),
            RowSpacing = 16
        };

        Grid.SetRow(body, 1);
        root.Children.Add(body);

        body.Children.Add(CreateField("影片網址", BuildUrlInputs(), 0));
        body.Children.Add(CreateField("輸出格式", BuildOutputFormatInput(), 1));

        var outputRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10
        };
        outputRow.Children.Add(_outputBox);
        Grid.SetColumn(_chooseFolderButton, 1);
        outputRow.Children.Add(_chooseFolderButton);
        body.Children.Add(CreateField("輸出資料夾", outputRow, 2));

        var actionRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 10
        };
        actionRow.Children.Add(_statusText);
        Grid.SetColumn(_clearUrlsButton, 1);
        actionRow.Children.Add(_clearUrlsButton);
        Grid.SetColumn(_convertButton, 2);
        actionRow.Children.Add(_convertButton);
        Grid.SetRow(actionRow, 3);
        body.Children.Add(actionRow);

        Grid.SetRow(_progressBar, 4);
        body.Children.Add(_progressBar);

        var logPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 8
        };
        logPanel.Children.Add(new TextBlock
        {
            Text = "記錄",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#394150")
        });
        Grid.SetRow(_logBox, 1);
        logPanel.Children.Add(_logBox);
        Grid.SetRow(logPanel, 5);
        body.Children.Add(logPanel);

        return root;
    }

    private static Control CreateField(string label, Control content, int row)
    {
        var panel = new StackPanel { Spacing = 7 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#394150")
        });
        panel.Children.Add(content);
        Grid.SetRow(panel, row);
        return panel;
    }

    private static TextBox CreateUrlBox(string label)
    {
        return new TextBox
        {
            PlaceholderText = $"{label}: 貼上 YouTube 或 Bilibili 影片/播放清單網址",
            FontSize = 15,
            MinHeight = 40
        };
    }

    private Control BuildUrlInputs()
    {
        var panel = new StackPanel { Spacing = 8 };

        var countRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        countRow.Children.Add(new TextBlock
        {
            Text = "網址組數",
            FontSize = 13,
            Foreground = Brush.Parse("#394150"),
            VerticalAlignment = VerticalAlignment.Center
        });
        countRow.Children.Add(_urlInputCountComboBox);
        panel.Children.Add(countRow);

        foreach (var urlBox in _urlBoxes)
        {
            panel.Children.Add(urlBox);
        }

        return panel;
    }

    private Control BuildOutputFormatInput()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(_outputFormatComboBox);
        row.Children.Add(new TextBlock
        {
            Text = "MP4 畫質",
            FontSize = 13,
            Foreground = Brush.Parse("#394150"),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(_mp4QualityComboBox);
        return row;
    }

    private async void ChooseFolderAsync(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "選擇輸出資料夾",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null && folder.TryGetLocalPath() is { } path)
        {
            _outputBox.Text = path;
            SaveOutputFolderIfValid();
            SetStatus("輸出資料夾已更新");
        }
    }

    private void ClearUrls(object? sender, RoutedEventArgs e)
    {
        foreach (var urlBox in _urlBoxes)
        {
            urlBox.Text = "";
        }

        SetStatus("網址已清除");
    }

    private void UrlInputCountChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_urlInputCountComboBox.SelectedItem is not int selectedCount)
        {
            return;
        }

        _urlInputCount = NormalizeUrlInputCount(selectedCount);
        UpdateUrlBoxVisibility();
        SaveSettingsIfPossible();
        SetStatus($"網址組數已改為 {_urlInputCount} 組");
    }

    private void OutputFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_outputFormatComboBox.SelectedItem is not string selectedFormat)
        {
            return;
        }

        _outputFormat = NormalizeOutputFormat(selectedFormat);
        SaveSettingsIfPossible();
        SetStatus($"輸出格式已改為 {_outputFormat}");
        UpdateMp4QualityVisibility();
        UpdateConvertButtonText();
    }

    private void Mp4QualityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_mp4QualityComboBox.SelectedItem is not string selectedQuality)
        {
            return;
        }

        _mp4Quality = NormalizeMp4Quality(selectedQuality);
        SaveSettingsIfPossible();
        SetStatus($"MP4 畫質已改為 {_mp4Quality}");
    }

    private async void ConvertOrCancelAsync(object? sender, RoutedEventArgs e)
    {
        if (_conversionTokenSource is not null)
        {
            _conversionTokenSource.Cancel();
            return;
        }

        var urls = _urlBoxes
            .Take(_urlInputCount)
            .Select(box => box.Text?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Cast<string>()
            .Select(NormalizeMediaUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (urls.Length == 0)
        {
            SetStatus("請至少輸入一個 YouTube 或 Bilibili 網址");
            return;
        }

        var outputPath = _outputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath))
        {
            SetStatus("請選擇有效的輸出資料夾");
            return;
        }
        AppSettings.Save(outputPath, _urlInputCount, _outputFormat, _mp4Quality);

        var ytDlpPath = ToolLocator.FindExecutable("yt-dlp");
        var ffmpegPath = ToolLocator.FindExecutable("ffmpeg");
        var ffprobePath = ToolLocator.FindExecutable("ffprobe");
        if (ytDlpPath is null || ffmpegPath is null || ffprobePath is null)
        {
            SetStatus("找不到轉檔工具，請先安裝後再試");
            AppendInstallHint();
            AppendLog($"yt-dlp: {ytDlpPath ?? "找不到"}");
            AppendLog($"ffmpeg: {ffmpegPath ?? "找不到"}");
            AppendLog($"ffprobe: {ffprobePath ?? "找不到"}");
            return;
        }

        _conversionTokenSource = new CancellationTokenSource();
        SetBusy(true);
        _logBox.Text = "";
        AppendLog($"yt-dlp: {ytDlpPath}");
        AppendLog($"ffmpeg: {ffmpegPath}");
        AppendLog($"ffprobe: {ffprobePath}");
        AppendLog($"輸出資料夾: {outputPath}");
        AppendLog($"輸出格式: {_outputFormat}");
        if (_outputFormat == "MP4")
        {
            AppendLog($"MP4 畫質: {_mp4Quality}");
        }
        AppendLog($"準備轉換 {urls.Length} 個項目");

        try
        {
            var successCount = 0;
            for (var index = 0; index < urls.Length; index++)
            {
                var current = index + 1;
                SetStatus($"正在轉換 {current}/{urls.Length}...");
                AppendLog("");
                AppendLog($"[{current}/{urls.Length}] {urls[index]}");

                var code = await RunYtDlpAsync(ytDlpPath, ffmpegPath, ffprobePath, urls[index], outputPath, _outputFormat, _mp4Quality, _conversionTokenSource.Token);
                if (code == 0)
                {
                    successCount++;
                }
                else
                {
                    AppendLog($"[{current}/{urls.Length}] 轉換失敗，結束碼 {code}");
                }
            }

            SetStatus(successCount == urls.Length
                ? $"完成，已輸出 {successCount} 個 {_outputFormat}"
                : $"完成 {successCount}/{urls.Length} 個，請查看記錄");
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消");
            AppendLog("使用者取消轉換。");
        }
        catch (Exception ex)
        {
            SetStatus("轉換時發生錯誤");
            AppendLog(ex.Message);
        }
        finally
        {
            _conversionTokenSource?.Dispose();
            _conversionTokenSource = null;
            SetBusy(false);
        }
    }

    private async Task<int> RunYtDlpAsync(
        string ytDlpPath,
        string ffmpegPath,
        string ffprobePath,
        string url,
        string outputPath,
        string outputFormat,
        string mp4Quality,
        CancellationToken token)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUTF8"] = "1";
        ToolLocator.PrependToPath(startInfo.Environment, ytDlpPath, ffmpegPath, ffprobePath);

        AddOutputFormatArguments(startInfo, outputFormat, mp4Quality);
        startInfo.ArgumentList.Add("--encoding");
        startInfo.ArgumentList.Add("utf-8");
        startInfo.ArgumentList.Add("--ffmpeg-location");
        startInfo.ArgumentList.Add(Path.GetDirectoryName(ffmpegPath) ?? ffmpegPath);
        AddBilibiliBrowserHeaders(startInfo, url);
        var cookieBrowser = AddBilibiliBrowserCookies(startInfo, url);
        if (cookieBrowser is not null)
        {
            AppendLog($"Bilibili cookies: {cookieBrowser}");
        }
        if (outputFormat == "MP3")
        {
            startInfo.ArgumentList.Add("--embed-thumbnail");
        }
        startInfo.ArgumentList.Add("--add-metadata");
        startInfo.ArgumentList.Add("--paths");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add("%(title)s.%(ext)s");
        startInfo.ArgumentList.Add(url);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => AppendLog(args.Data);
        process.ErrorDataReceived += (_, args) => AppendLog(args.Data);

        if (!process.Start())
        {
            throw new InvalidOperationException("無法啟動 yt-dlp。");
        }

        var outputTask = ReadProcessStreamAsync(process.StandardOutput.BaseStream, token);
        var errorTask = ReadProcessStreamAsync(process.StandardError.BaseStream, token);

        try
        {
            await process.WaitForExitAsync(token);
            await Task.WhenAll(outputTask, errorTask);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        return process.ExitCode;
    }

    private static void AddOutputFormatArguments(ProcessStartInfo startInfo, string outputFormat, string mp4Quality)
    {
        if (outputFormat == "MP4")
        {
            startInfo.ArgumentList.Add("--format");
            startInfo.ArgumentList.Add(GetMp4FormatSelector(mp4Quality));
            startInfo.ArgumentList.Add("--merge-output-format");
            startInfo.ArgumentList.Add("mp4");
            return;
        }

        startInfo.ArgumentList.Add("--extract-audio");
        startInfo.ArgumentList.Add("--audio-format");
        startInfo.ArgumentList.Add("mp3");
        startInfo.ArgumentList.Add("--audio-quality");
        startInfo.ArgumentList.Add("0");
    }

    private static string GetMp4FormatSelector(string mp4Quality)
    {
        var maxHeight = mp4Quality == "4K" ? 2160 : 1080;
        return $"bestvideo*[height<={maxHeight}]+bestaudio/best[height<={maxHeight}]/best";
    }

    private static void AddBilibiliBrowserHeaders(ProcessStartInfo startInfo, string url)
    {
        if (!IsBilibiliVideoUrl(url))
        {
            return;
        }

        startInfo.ArgumentList.Add("--add-headers");
        startInfo.ArgumentList.Add("User-Agent:Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        startInfo.ArgumentList.Add("--add-headers");
        startInfo.ArgumentList.Add("Referer:https://www.bilibili.com/");
        startInfo.ArgumentList.Add("--add-headers");
        startInfo.ArgumentList.Add("Accept-Language:zh-CN,zh-TW;q=0.9,zh;q=0.8,en;q=0.7");
    }

    private static string? AddBilibiliBrowserCookies(ProcessStartInfo startInfo, string url)
    {
        if (!IsBilibiliVideoUrl(url))
        {
            return null;
        }

        var browser = FindBrowserForCookies();
        if (browser is null)
        {
            return null;
        }

        startInfo.ArgumentList.Add("--cookies-from-browser");
        startInfo.ArgumentList.Add(browser);
        return browser;
    }

    private static string? FindBrowserForCookies()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (Directory.Exists("/Applications/Firefox.app"))
            {
                return "firefox";
            }

            if (Directory.Exists("/Applications/Google Chrome.app"))
            {
                return "chrome";
            }

            if (Directory.Exists("/Applications/Safari.app"))
            {
                return "safari";
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (Directory.Exists(Path.Combine(localAppData, "Mozilla", "Firefox"))
                || Directory.Exists(Path.Combine(programFiles, "Mozilla Firefox"))
                || Directory.Exists(Path.Combine(programFilesX86, "Mozilla Firefox")))
            {
                return "firefox";
            }

            if (Directory.Exists(Path.Combine(localAppData, "Google", "Chrome"))
                || Directory.Exists(Path.Combine(programFiles, "Google", "Chrome"))
                || Directory.Exists(Path.Combine(programFilesX86, "Google", "Chrome")))
            {
                return "chrome";
            }

            if (Directory.Exists(Path.Combine(localAppData, "Microsoft", "Edge"))
                || Directory.Exists(Path.Combine(programFiles, "Microsoft", "Edge"))
                || Directory.Exists(Path.Combine(programFilesX86, "Microsoft", "Edge")))
            {
                return "edge";
            }
        }

        return null;
    }

    private static string NormalizeMediaUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        if (!IsBilibiliVideoUri(uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri)
        {
            Query = RemoveTrackingQueryParameters(uri.Query)
        };

        return builder.Uri.ToString();
    }

    private static bool IsBilibiliVideoUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && IsBilibiliVideoUri(uri);
    }

    private static bool IsBilibiliVideoUri(Uri uri)
    {
        return uri.Host.EndsWith("bilibili.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/video/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveTrackingQueryParameters(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "";
        }

        var trackingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "spm_id_from",
            "from_spmid",
            "vd_source",
            "share_source",
            "share_medium",
            "share_plat",
            "share_session_id",
            "unique_k"
        };

        var keptParameters = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(parameter =>
            {
                var key = parameter.Split('=', 2)[0];
                return !trackingKeys.Contains(Uri.UnescapeDataString(key));
            });

        return string.Join("&", keptParameters);
    }

    private async Task ReadProcessStreamAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[4096];
        var pending = new List<byte>();

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                var value = buffer[index];
                if (value == (byte)'\n')
                {
                    AppendDecodedLogLine(pending);
                    pending.Clear();
                    continue;
                }

                pending.Add(value);
            }
        }

        AppendDecodedLogLine(pending);
    }

    private void AppendDecodedLogLine(List<byte> bytes)
    {
        while (bytes.Count > 0 && bytes[^1] == (byte)'\r')
        {
            bytes.RemoveAt(bytes.Count - 1);
        }

        if (bytes.Count == 0)
        {
            return;
        }

        var line = DecodeProcessText(bytes.ToArray());
        AppendLog(line);

        if (line.Contains("HTTP Error 412", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Precondition Failed", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog("Bilibili 回傳 412，通常是網站防護、地區限制、會員/登入限制或瀏覽器 cookies 無法讀取。請先確認瀏覽器可正常播放該影片，並關閉瀏覽器後再試一次。");
        }
    }

    private static string DecodeProcessText(byte[] bytes)
    {
        try
        {
            return Utf8Strict.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return SystemAnsiEncoding.GetString(bytes);
        }
    }

    private static Encoding GetSystemAnsiEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private void CheckTools()
    {
        var ytDlp = ToolLocator.FindExecutable("yt-dlp");
        var ffmpeg = ToolLocator.FindExecutable("ffmpeg");
        var ffprobe = ToolLocator.FindExecutable("ffprobe");

        if (ytDlp is null || ffmpeg is null || ffprobe is null)
        {
            SetStatus("需要 yt-dlp 和 ffmpeg 才能轉換 MP3 / MP4");
            AppendInstallHint();
            AppendLog($"yt-dlp: {ytDlp ?? "找不到"}");
            AppendLog($"ffmpeg: {ffmpeg ?? "找不到"}");
            AppendLog($"ffprobe: {ffprobe ?? "找不到"}");
            return;
        }

        AppendLog($"yt-dlp: {ytDlp}");
        AppendLog($"ffmpeg: {ffmpeg}");
        AppendLog($"ffprobe: {ffprobe}");
    }

    private void AppendInstallHint()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AppendLog("Windows 第一次使用前，請先安裝轉檔工具：");
            AppendLog("1. 按 Windows 鍵，輸入「終端機」");
            AppendLog("2. 在「終端機」上按右鍵，選擇「以系統管理員身分執行」");
            AppendLog("3. 貼上這行指令後按 Enter：");
            AppendLog("   winget install yt-dlp.yt-dlp Gyan.FFmpeg");
            AppendLog("4. 如果畫面詢問是否同意，輸入 Y 後按 Enter");
            AppendLog("5. 安裝完成後，重新開啟這個程式");
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            AppendLog("macOS 可用 Homebrew 安裝：brew install yt-dlp ffmpeg");
            return;
        }

        AppendLog("請用系統套件管理器安裝 yt-dlp 和 ffmpeg，並確認兩者可從 PATH 執行。");
    }

    private void SetBusy(bool busy)
    {
        _progressBar.IsIndeterminate = busy;
        _chooseFolderButton.IsEnabled = !busy;
        _clearUrlsButton.IsEnabled = !busy;
        _urlInputCountComboBox.IsEnabled = !busy;
        _outputFormatComboBox.IsEnabled = !busy;
        _mp4QualityComboBox.IsEnabled = !busy;
        foreach (var urlBox in _urlBoxes)
        {
            urlBox.IsEnabled = !busy;
        }
        _outputBox.IsEnabled = !busy;
        UpdateConvertButtonText(busy);
    }

    private void SaveOutputFolderIfValid()
    {
        var outputPath = _outputBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(outputPath) && Directory.Exists(outputPath))
        {
            SaveSettingsIfPossible();
        }
    }

    private void SaveSettingsIfPossible()
    {
        var outputPath = _outputBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(outputPath) && Directory.Exists(outputPath))
        {
            AppSettings.Save(outputPath, _urlInputCount, _outputFormat, _mp4Quality);
            return;
        }

        AppSettings.Save(AppSettings.GetDefaultOutputFolder(), _urlInputCount, _outputFormat, _mp4Quality);
    }

    private void UpdateUrlBoxVisibility()
    {
        for (var index = 0; index < _urlBoxes.Length; index++)
        {
            _urlBoxes[index].IsVisible = index < _urlInputCount;
        }
    }

    private void UpdateConvertButtonText(bool busy = false)
    {
        _convertButton.Content = busy ? "取消" : $"轉成 {_outputFormat}";
    }

    private void UpdateMp4QualityVisibility()
    {
        _mp4QualityComboBox.IsVisible = _outputFormat == "MP4";
    }

    private static int NormalizeUrlInputCount(int count)
    {
        return UrlInputCountOptions.Contains(count) ? count : 1;
    }

    private static string NormalizeOutputFormat(string? format)
    {
        return OutputFormatOptions.Contains(format, StringComparer.OrdinalIgnoreCase)
            ? format!.ToUpperInvariant()
            : "MP3";
    }

    private static string NormalizeMp4Quality(string? quality)
    {
        return Mp4QualityOptions.Contains(quality, StringComparer.OrdinalIgnoreCase)
            ? quality!
            : "1080p";
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
    }

    private void AppendLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _logBox.Text += $"{line}{Environment.NewLine}";
            _logBox.CaretIndex = _logBox.Text.Length;
        });
    }
}

internal sealed class AppSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YoutubeOrBilibiliMP3Converter");

    private static readonly string LegacySettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YoutubeToMP3Converter",
        "settings.json");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public string LastOutputFolder { get; init; } = GetDefaultOutputFolder();
    public int UrlInputCount { get; init; } = 1;
    public string OutputFormat { get; init; } = "MP3";
    public string Mp4Quality { get; init; } = "1080p";

    public static AppSettings Load()
    {
        try
        {
            var path = File.Exists(SettingsPath)
                ? SettingsPath
                : LegacySettingsPath;

            if (File.Exists(path))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (settings is not null)
                {
                    return new AppSettings
                    {
                        LastOutputFolder = Directory.Exists(settings.LastOutputFolder)
                            ? settings.LastOutputFolder
                            : GetDefaultOutputFolder(),
                        UrlInputCount = NormalizeUrlInputCount(settings.UrlInputCount),
                        OutputFormat = NormalizeOutputFormat(settings.OutputFormat),
                        Mp4Quality = NormalizeMp4Quality(settings.Mp4Quality)
                    };
                }
            }
        }
        catch
        {
            // Invalid settings should not stop the app from opening.
        }

        return new AppSettings();
    }

    public static void Save(string outputFolder, int urlInputCount, string outputFormat, string mp4Quality)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var settings = new AppSettings
            {
                LastOutputFolder = outputFolder,
                UrlInputCount = NormalizeUrlInputCount(urlInputCount),
                OutputFormat = NormalizeOutputFormat(outputFormat),
                Mp4Quality = NormalizeMp4Quality(mp4Quality)
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // The converter can still work even if preferences cannot be saved.
        }
    }

    public static string GetDefaultOutputFolder()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        return Directory.Exists(downloads)
            ? downloads
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static int NormalizeUrlInputCount(int count)
    {
        return count is 1 or 3 or 7 ? count : 1;
    }

    private static string NormalizeOutputFormat(string? format)
    {
        return string.Equals(format, "MP4", StringComparison.OrdinalIgnoreCase) ? "MP4" : "MP3";
    }

    private static string NormalizeMp4Quality(string? quality)
    {
        return string.Equals(quality, "4K", StringComparison.OrdinalIgnoreCase) ? "4K" : "1080p";
    }
}

internal static class ToolLocator
{
    private static readonly string[] UnixSearchPaths =
    [
        "/opt/homebrew/bin",
        "/usr/local/bin",
        "/usr/bin",
        "/bin"
    ];

    public static string? FindExecutable(string name)
    {
        var executableNames = GetExecutableNames(name);
        var searchPaths = GetSearchPaths();

        foreach (var path in searchPaths)
        {
            foreach (var executableName in executableNames)
            {
                var candidate = Path.Combine(path, executableName);
                if (File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static void PrependToPath(IDictionary<string, string?> environment, params string[] executablePaths)
    {
        var directories = executablePaths
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (directories.Length == 0)
        {
            return;
        }

        var existingPath = environment.TryGetValue("PATH", out var path)
            ? path
            : Environment.GetEnvironmentVariable("PATH");

        environment["PATH"] = string.Join(Path.PathSeparator, directories.Concat(
            (existingPath ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)));
    }

    private static IEnumerable<string> GetExecutableNames(string name)
    {
        yield return name;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Path.HasExtension(name))
        {
            yield break;
        }

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var extension in extensions)
        {
            yield return $"{name}{extension.ToLowerInvariant()}";
        }
    }

    private static IEnumerable<string> GetSearchPaths()
    {
        IEnumerable<string> paths = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            paths = paths.Concat(UnixSearchPaths);
        }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
