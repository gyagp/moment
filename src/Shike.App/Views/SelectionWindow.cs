using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Shike.App.Services;
using Shike.Core;
using Windows.Foundation;
using Windows.System;

namespace Shike.App.Views;

internal sealed record Selection(CaptureRect Region, PixelFrame Frame);

internal sealed class SelectionWindow : Window
{
    private readonly TaskCompletionSource<Selection?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Rectangle _outline = new() { Stroke = new SolidColorBrush(ColorHelper.FromArgb(255, 90, 225, 195)), StrokeThickness = 2 };
    private readonly CaptureRect _bounds;
    private readonly PixelFrame _frame;
    private Point? _start;
    private Point _end;
    internal Task<Selection?> Result => _result.Task;

    internal SelectionWindow(DisplayInfo display, PixelFrame frame)
    {
        _bounds = display.Bounds;
        _frame = frame;
        Title = "拖动选择区域 · Esc 取消";
        var root = new Grid();
        root.Children.Add(new Image { Source = ImageStore.ToBitmap(frame), Stretch = Stretch.Fill });
        root.Children.Add(new Rectangle { Fill = new SolidColorBrush(Colors.Black), Opacity = 0.28 });
        root.Children.Add(_canvas);
        _canvas.Children.Add(_outline);
        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(235, 20, 26, 32)),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 10, 18, 10),
            Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Child = new TextBlock { Text = "拖动选择区域   ·   Esc 取消", Foreground = new SolidColorBrush(Colors.White), FontSize = 16 }
        });
        Content = root;
        _canvas.PointerPressed += Pressed;
        _canvas.PointerMoved += Moved;
        _canvas.PointerReleased += Released;
        _canvas.PointerCanceled += (_, _) => Close();
        // Window-level accelerator also works before any focusable control is clicked.
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, e) => { e.Handled = true; Close(); };
        root.KeyboardAccelerators.Add(escape);
        Closed += (_, _) => _result.TrySetResult(null);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.MoveAndResize(new(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height));
    }

    private Point Clamp(Point point) => new(Math.Clamp(point.X, 0, _canvas.ActualWidth), Math.Clamp(point.Y, 0, _canvas.ActualHeight));
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed) return;
        _start = Clamp(e.GetCurrentPoint(_canvas).Position);
        _end = _start.Value;
        _canvas.CapturePointer(e.Pointer);
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (_start is not { } start) return;
        _end = Clamp(e.GetCurrentPoint(_canvas).Position);
        Canvas.SetLeft(_outline, Math.Min(start.X, _end.X));
        Canvas.SetTop(_outline, Math.Min(start.Y, _end.Y));
        _outline.Width = Math.Abs(start.X - _end.X);
        _outline.Height = Math.Abs(start.Y - _end.Y);
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (_start is not { } start || _canvas.ActualWidth == 0 || _canvas.ActualHeight == 0) return;
        _end = Clamp(e.GetCurrentPoint(_canvas).Position);
        _canvas.ReleasePointerCapture(e.Pointer);
        var xScale = _frame.Width / _canvas.ActualWidth;
        var yScale = _frame.Height / _canvas.ActualHeight;
        var x = (int)Math.Floor(Math.Min(start.X, _end.X) * xScale);
        var y = (int)Math.Floor(Math.Min(start.Y, _end.Y) * yScale);
        var right = Math.Min(_frame.Width, (int)Math.Ceiling(Math.Max(start.X, _end.X) * xScale));
        var bottom = Math.Min(_frame.Height, (int)Math.Ceiling(Math.Max(start.Y, _end.Y) * yScale));
        if (right - x < 8 || bottom - y < 8) { _start = null; return; }
        _result.TrySetResult(new(new(_bounds.X + x, _bounds.Y + y, right - x, bottom - y), _frame.Crop(x, y, right - x, bottom - y)));
        Close();
    }
}
