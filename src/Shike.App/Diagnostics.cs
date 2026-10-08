using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ScreenRecorderLib;
using Shike.App.Services;
using Shike.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Shike.App;

public sealed partial class MainWindow
{
    /// <summary>Opt-in integration diagnostic. Never runs on ordinary launch.
    /// Captures only this test window's interior, disables both audio sources.</summary>
    internal async Task RunSmokeTestAsync(string output)
    {
        var display = NativeMethods.GetDisplays().First();
        AppWindow.Move(new(display.Bounds.X + 30, display.Bounds.Y + 30));
        ((Microsoft.UI.Windowing.OverlappedPresenter)AppWindow.Presenter).IsAlwaysOnTop = true;
        AppWindow.Show();
        NativeMethods.ShowWindow(_handle, 9);
        Activate();
        NativeMethods.SetForegroundWindow(_handle);
        await Task.Delay(1200);
        var pos = AppWindow.Position;
        var size = AppWindow.Size;
        var ui = NativeMethods.Capture(new(pos.X, pos.Y, size.Width, size.Height));
        var uiPath = await ImageStore.SaveAsync(ui, output);
        File.Move(uiPath, Path.Combine(output, "ui.png"), true);

        var lines = new StackPanel();
        var random = new Random(7412);
        for (var i = 0; i < 80; i++)
            lines.Children.Add(new Border
            {
                Height = 40,
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, (byte)random.Next(45, 125), (byte)random.Next(75, 170), (byte)random.Next(90, 180))),
                Child = new TextBlock { Text = $"时刻 · 渲染测试 {i:D3}   /   {random.Next():X8}", FontSize = 19, Foreground = new SolidColorBrush(Colors.White), Margin = new Thickness(16, 6, 0, 0) }
            });
        var scroll = new ScrollViewer
        {
            Width = 600, Height = 400, Content = lines,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        Content = new Grid { Padding = new Thickness(40), Children = { scroll } };
        await Task.Delay(500);
        var scale = scroll.XamlRoot.RasterizationScale;
        var origin = new NativeMethods.Point();
        NativeMethods.ClientToScreen(_handle, ref origin);
        var region = new CaptureRect(origin.X + (int)(40 * scale), origin.Y + (int)(40 * scale), (int)(600 * scale), (int)(400 * scale));
        var stitcher = new ScrollStitcher();
        stitcher.Add(NativeMethods.Capture(region));
        scroll.ChangeView(null, 120, null, true);
        await Task.Delay(400);
        var result = stitcher.Add(NativeMethods.Capture(region));
        if (result.Status != StitchStatus.Added || Math.Abs(result.AddedRows - 120 * scale) > 1)
            throw new InvalidOperationException($"Rendered scroll did not match: {result}, scale={scale}, offset={scroll.VerticalOffset}");
        var image = stitcher.Build();
        var imagePath = await ImageStore.SaveAsync(image, output);
        var file = await StorageFile.GetFileFromPathAsync(imagePath);
        using (var stream = await file.OpenReadAsync())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            if (decoder.PixelWidth != image.Width || decoder.PixelHeight != image.Height) throw new Exception("PNG dimensions mismatch");
        }
        using var recording = new RecordingSession();
        var began = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hasStarted = false;
        recording.StatusChanged += status =>
        {
            if (status == RecorderStatus.Recording) { if (hasStarted) resumed.TrySetResult(); else { hasStarted = true; began.TrySetResult(); } }
            if (status == RecorderStatus.Paused) paused.TrySetResult();
        };
        var crop = new CaptureRect(region.X - display.Bounds.X, region.Y - display.Bounds.Y, region.Width, region.Height);
        recording.Start(display, false, false, output, crop);
        if (await Task.WhenAny(began.Task, recording.Completion, Task.Delay(15000)) != began.Task)
        {
            if (recording.Completion.IsCompleted) await recording.Completion;
            throw new TimeoutException("Recorder did not start");
        }
        await Task.Delay(1200);
        recording.Pause();
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        recording.Resume();
        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        scroll.ChangeView(null, 220, null, true);
        await Task.Delay(1300);
        recording.Stop();
        var videoPath = await recording.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        var video = await StorageFile.GetFileFromPathAsync(videoPath);
        var properties = await video.Properties.GetVideoPropertiesAsync();
        if (properties.Duration.TotalSeconds < 1 || properties.Width == 0 || properties.Height == 0)
            throw new Exception("MP4 metadata invalid");
        var clip = await Windows.Media.Editing.MediaClip.CreateFromFileAsync(video);
        var composition = new Windows.Media.Editing.MediaComposition();
        composition.Clips.Add(clip);
        using var thumbnail = await composition.GetThumbnailAsync(TimeSpan.FromMilliseconds(500), 160, 100, Windows.Media.Editing.VideoFramePrecision.NearestFrame);
        var thumbnailDecoder = await BitmapDecoder.CreateAsync(thumbnail);
        if (thumbnailDecoder.PixelWidth == 0) throw new Exception("MP4 frame decode failed");
        File.WriteAllText(Path.Combine(output, "video-metadata.txt"), $"{properties.Width}x{properties.Height}, {properties.Duration}, {properties.Bitrate} bps\n");
    }
}
