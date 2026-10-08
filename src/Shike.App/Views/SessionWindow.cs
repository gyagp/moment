using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shike.App.Services;

namespace Shike.App.Views;

internal sealed class SessionWindow : Window
{
    private readonly TextBlock _status = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
    private bool _finishing;
    internal SessionWindow(DisplayInfo display, string message, Action stop)
    {
        Title = "时刻 · 捕获中";
        _status.Text = message;
        var panel = new StackPanel { Padding = new Thickness(14), Spacing = 8 };
        panel.Children.Add(_status);
        var button = new Button { Content = "结束并保存  (Ctrl + Shift + F)", HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => { button.IsEnabled = false; stop(); };
        panel.Children.Add(button);
        Content = panel;
        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.Move(new(display.Bounds.X + Math.Max(0, display.Bounds.Width - 520), display.Bounds.Y + 32));
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = Math.Max(1, NativeMethods.GetDpiForWindow(handle) / 96.0);
        var width = (int)(370 * scale);
        var height = (int)(160 * scale);
        AppWindow.MoveAndResize(new(display.Bounds.X + Math.Max(0, display.Bounds.Width - width - 24), display.Bounds.Y + 32, width, height));
        NativeMethods.SetWindowDisplayAffinity(handle, 0x11);
        AppWindow.Closing += (_, e) => { if (!_finishing) { e.Cancel = true; stop(); } };
    }
    internal void SetStatus(string text) => _status.Text = text;
    internal void Finish() { _finishing = true; Close(); }
}
