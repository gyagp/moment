using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ScreenRecorderLib;
using Shike.App.Services;
using Shike.App.Views;
using Shike.Core;

namespace Shike.App;

public sealed partial class MainWindow : Window
{
    private enum CaptureMode { Idle, Selecting, Screenshot, Scrolling, Recording, Finalizing }
    private CaptureMode _mode;
    private readonly nint _handle;
    private readonly NativeMethods.SubclassProc _windowProc;
    private readonly List<int> _hotkeys = [];
    private RecordingSession? _recording;
    private CancellationTokenSource? _scrollCancellation;
    private SessionWindow? _sessionWindow;
    private SelectionWindow? _selectionWindow;
    private bool _paused, _recordingStarted, _closeWhenDone;
    private string? _resultPath;
    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Shike.ico"));
        AppWindow.Resize(new(1030, 960));
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _windowProc = WindowProc;
        NativeMethods.SetWindowSubclass(_handle, _windowProc, 1, 0);
        var failed = new List<string>();
        foreach (var (id, key, name) in new[] { (1, 0x53u, "S"), (2, 0x4Cu, "L"), (3, 0x52u, "R"), (4, 0x46u, "F") })
        {
            if (NativeMethods.RegisterHotKey(_handle, id, 0x4006, key)) _hotkeys.Add(id);
            else failed.Add($"Ctrl + Shift + {name}");
        }
        if (failed.Count > 0) ShortcutText.Text = $"快捷键被占用：{string.Join("、", failed)}。仍可使用界面按钮。";
        RefreshDisplays();
        _timer.Tick += (_, _) =>
        {
            if (_mode != CaptureMode.Recording) return;
            var text = $"{(_paused ? "已暂停" : "正在录屏")} · {_elapsed.Elapsed:hh\\:mm\\:ss}";
            StatusBar.Title = text;
            _sessionWindow?.SetStatus(text + "\n录制所选显示器 · MP4");
        };
        AppWindow.Closing += (_, e) =>
        {
            if (_mode == CaptureMode.Idle) return;
            e.Cancel = true;
            _closeWhenDone = true;
            StopActive();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            foreach (var id in _hotkeys) NativeMethods.UnregisterHotKey(_handle, id);
            NativeMethods.RemoveWindowSubclass(_handle, _windowProc, 1);
        };
    }

    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == 0x0312)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                switch ((int)wParam)
                {
                    case 1: _ = RunAsync(CaptureScreenshotAsync); break;
                    case 2: _ = RunAsync(CaptureScrollAsync); break;
                    case 3:
                        if (_mode == CaptureMode.Recording) StopActive();
                        else _ = RunAsync(RecordAsync);
                        break;
                    case 4: StopActive(); break;
                }
            });
            return 0;
        }
        return NativeMethods.DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void RefreshDisplays()
    {
        var selected = (DisplayPicker.SelectedItem as DisplayInfo)?.DeviceName;
        var displays = NativeMethods.GetDisplays();
        DisplayPicker.ItemsSource = displays;
        DisplayPicker.SelectedItem = displays.Find(d => d.DeviceName == selected) ?? displays.FirstOrDefault();
    }
    private DisplayInfo SelectedDisplay => DisplayPicker.SelectedItem as DisplayInfo ?? throw new InvalidOperationException("未找到可捕获的显示器，请刷新。");

    private void SetMode(CaptureMode mode)
    {
        _mode = mode;
        var idle = mode == CaptureMode.Idle;
        ScreenshotButton.IsEnabled = ScrollButton.IsEnabled = RecordButton.IsEnabled = idle;
        DisplayPicker.IsEnabled = RefreshButton.IsEnabled = SystemAudioCheck.IsEnabled = MicrophoneCheck.IsEnabled = idle;
        SessionControls.Visibility = mode is CaptureMode.Recording or CaptureMode.Scrolling or CaptureMode.Finalizing ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Visibility = mode == CaptureMode.Recording ? Visibility.Visible : Visibility.Collapsed;
        StopButton.IsEnabled = mode is CaptureMode.Recording or CaptureMode.Scrolling;
    }
    private void Status(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        StatusBar.Title = title; StatusBar.Message = message; StatusBar.Severity = severity; StatusBar.IsOpen = true;
    }
    private void RestoreWindow()
    {
        AppWindow.Show();
        NativeMethods.ShowWindow(_handle, 9);
        Activate();
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_mode != CaptureMode.Idle) return;
        SetMode(CaptureMode.Selecting);
        try { await action(); }
        catch (Exception error) { Status("捕获未完成", error.Message, InfoBarSeverity.Error); }
        finally
        {
            _timer.Stop();
            _sessionWindow?.Finish(); _sessionWindow = null;
            _recording?.Dispose(); _recording = null;
            _scrollCancellation?.Dispose(); _scrollCancellation = null;
            SetMode(CaptureMode.Idle);
            RestoreWindow();
            if (_closeWhenDone) Close();
        }
    }

    private async Task<Selection?> SelectAsync(DisplayInfo display)
    {
        AppWindow.Hide();
        await Task.Delay(250);
        var frame = await Task.Run(() => NativeMethods.Capture(display.Bounds));
        _selectionWindow = new(display, frame);
        _selectionWindow.Activate();
        var selection = await _selectionWindow.Result;
        _selectionWindow = null;
        if (selection is null) Status("已取消", "没有保存文件。");
        return selection;
    }
    private async Task CaptureScreenshotAsync()
    {
        var selection = await SelectAsync(SelectedDisplay);
        if (selection is null) return;
        SetMode(CaptureMode.Screenshot);
        var path = await ImageStore.SaveAsync(selection.Frame);
        ShowResult(path, true);
        Status("截图已保存", $"{selection.Frame.Width} × {selection.Frame.Height} 像素", InfoBarSeverity.Success);
    }

    private async Task CaptureScrollAsync()
    {
        var display = SelectedDisplay;
        var selection = await SelectAsync(display);
        if (selection is null) return;
        SetMode(CaptureMode.Scrolling);
        _scrollCancellation = new();
        var token = _scrollCancellation.Token;
        var stitcher = new ScrollStitcher();
        if (stitcher.Add(selection.Frame).Status == StitchStatus.LimitReached)
            throw new InvalidOperationException("所选区域过大，请选择较小的区域。");
        _sessionWindow = new(display, "缓慢向下滚动，每次停留片刻。\n请保持窗口位置不变。", StopActive);
        _sessionWindow.Activate();
        Status("滚动截图中", "缓慢向下滚动，点击浮动条中的“结束并保存”或按 Ctrl + Shift + F。不要移动目标窗口。");
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(800, token); } catch (OperationCanceledException) { break; }
            var frame = await Task.Run(() => NativeMethods.Capture(selection.Region));
            if (token.IsCancellationRequested) break;
            var result = await Task.Run(() => stitcher.Add(frame));
            var hint = result.Status switch
            {
                StitchStatus.NoMatch => "未匹配：请向上回退少许，再缓慢向下滚动。",
                StitchStatus.LimitReached => "已达到长图大小上限，正在保存。",
                _ => "缓慢向下滚动，每次停留片刻。"
            };
            _sessionWindow.SetStatus($"长图高度 {result.TotalHeight:N0} px\n{hint}");
            if (result.Status == StitchStatus.LimitReached) break;
        }
        SetMode(CaptureMode.Finalizing);
        var image = await Task.Run(stitcher.Build);
        var path = await ImageStore.SaveAsync(image);
        ShowResult(path, true);
        Status("长图已保存", $"{image.Width} × {image.Height} 像素。滚动截图为实验功能，请检查拼接结果。", InfoBarSeverity.Success);
    }

    private async Task RecordAsync()
    {
        var display = SelectedDisplay;
        var systemAudio = SystemAudioCheck.IsChecked == true;
        var microphone = MicrophoneCheck.IsChecked == true;
        _paused = _recordingStarted = false;
        PauseButton.Content = "暂停录制";
        PauseButton.IsEnabled = false;
        _elapsed.Reset();
        _recording = new();
        _recording.StatusChanged += status => DispatcherQueue.TryEnqueue(() =>
        {
            if (_mode != CaptureMode.Recording) return;
            if (status == RecorderStatus.Recording) { _recordingStarted = true; _elapsed.Start(); PauseButton.IsEnabled = true; }
            else if (status == RecorderStatus.Paused) _elapsed.Stop();
        });
        NativeMethods.ShowWindow(_handle, 6);
        await Task.Delay(300);
        SetMode(CaptureMode.Recording);
        Status("正在启动录屏", "点击浮动条中的“结束并保存”或按 Ctrl + Shift + F 结束。可回到主窗口暂停。", InfoBarSeverity.Warning);
        _recording.Start(display, systemAudio, microphone);
        _sessionWindow = new(display, "正在启动录屏…", StopActive);
        _sessionWindow.Activate();
        _timer.Start();
        var path = await _recording.Completion;
        _elapsed.Stop();
        ShowResult(path, false);
        Status("录屏已保存", $"MP4 · {_elapsed.Elapsed:hh\\:mm\\:ss}", InfoBarSeverity.Success);
    }

    private void StopActive()
    {
        _selectionWindow?.Close();
        if (_mode == CaptureMode.Scrolling) _scrollCancellation?.Cancel();
        if (_mode == CaptureMode.Recording)
        {
            SetMode(CaptureMode.Finalizing);
            _elapsed.Stop();
            Status("正在保存录屏", "正在完成 MP4 文件，请稍候。");
            _sessionWindow?.SetStatus("正在完成 MP4 文件，请稍候…");
            _recording?.Stop();
        }
    }
    private void ShowResult(string path, bool image)
    {
        _resultPath = path;
        EmptyText.Visibility = Visibility.Collapsed;
        ResultText.Text = path;
        ResultActions.Visibility = Visibility.Visible;
        CopyButton.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
        PreviewImage.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
        PreviewImage.Source = image ? new BitmapImage(new Uri(path)) { DecodePixelWidth = 900 } : null;
    }
    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    private void SafeAction(Action action)
    {
        try { action(); } catch (Exception error) { Status("操作未完成", error.Message, InfoBarSeverity.Error); }
    }
    private async void Screenshot_Click(object sender, RoutedEventArgs e) => await RunAsync(CaptureScreenshotAsync);
    private async void Scroll_Click(object sender, RoutedEventArgs e) => await RunAsync(CaptureScrollAsync);
    private async void Record_Click(object sender, RoutedEventArgs e) => await RunAsync(RecordAsync);
    private void Stop_Click(object sender, RoutedEventArgs e) => StopActive();
    private void Refresh_Click(object sender, RoutedEventArgs e) => SafeAction(RefreshDisplays);
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_mode != CaptureMode.Recording || !_recordingStarted) return;
        SafeAction(() =>
        {
            if (_paused) _recording?.Resume(); else _recording?.Pause();
            _paused = !_paused;
            PauseButton.Content = _paused ? "继续录制" : "暂停录制";
        });
    }
    private void ImageFolder_Click(object sender, RoutedEventArgs e) => SafeAction(() => OpenFolder(ImageStore.ImageDirectory));
    private void VideoFolder_Click(object sender, RoutedEventArgs e) => SafeAction(() => OpenFolder(ImageStore.VideoDirectory));
    private void OpenResult_Click(object sender, RoutedEventArgs e) => SafeAction(() =>
    {
        if (_resultPath is not null) Process.Start(new ProcessStartInfo(_resultPath) { UseShellExecute = true });
    });
    private void RevealResult_Click(object sender, RoutedEventArgs e) => SafeAction(() =>
    {
        if (_resultPath is not null) Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{_resultPath}\"", UseShellExecute = true });
    });
    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_resultPath is null) return;
        try { await ImageStore.CopyAsync(_resultPath); Status("已复制图片", "可以粘贴到聊天、文档或图片编辑器中。", InfoBarSeverity.Success); }
        catch (Exception error) { Status("复制未完成", error.Message, InfoBarSeverity.Error); }
    }
}
