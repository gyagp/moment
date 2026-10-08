using Microsoft.UI.Xaml;

namespace Shike.App;

public partial class App : Application
{
    private Window? _window;
    public App() => InitializeComponent();
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        var testIndex = Array.IndexOf(arguments, "--smoke-test");
        var startupOnly = false;
        if (testIndex < 0) { testIndex = Array.IndexOf(arguments, "--startup-test"); startupOnly = testIndex >= 0; }
        if (testIndex < 0)
        {
            _window = new MainWindow();
            _window.Activate();
            return;
        }
        var output = testIndex + 1 < arguments.Length ? Path.GetFullPath(arguments[testIndex + 1]) : Path.GetFullPath("artifacts/smoke");
        Directory.CreateDirectory(output);
        try
        {
            var main = new MainWindow();
            _window = main;
            main.Activate();
            if (startupOnly) await Task.Delay(500); else await main.RunSmokeTestAsync(output);
            File.WriteAllText(Path.Combine(output, "result.txt"), startupOnly ? "PASS: packaged WinUI startup and resources.\n" : "PASS: WinUI launch; rectangle/fullscreen/freeform/window modes; cancellation; transparent PNG; occlusion-free HWND capture; automatic wheel scrolling/stitching/bottom detection/cancellation; MP4 pause/resume/finalize/frame decode.\n");
            Environment.ExitCode = 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "result.txt"), error.ToString());
            Environment.ExitCode = 1;
        }
        finally { if (_window is not null) _window.Close(); else Exit(); }
    }
}
