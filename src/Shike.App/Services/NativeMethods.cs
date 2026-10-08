using System.ComponentModel;
using System.Runtime.InteropServices;
using Shike.Core;

namespace Shike.App.Services;

public readonly record struct CaptureRect(int X, int Y, int Width, int Height);
public sealed record DisplayInfo(string DeviceName, CaptureRect Bounds, bool IsPrimary)
{
    public string Label => $"{(IsPrimary ? "主显示器" : "显示器")} · {Bounds.Width} × {Bounds.Height} ({DeviceName})";
}

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, ImageSize;
        public int XPels, YPels;
        public uint Used, Important;
    }
    private delegate bool MonitorEnum(nint monitor, nint dc, ref Rect bounds, nint data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(nint target, int x, int y, int w, int h, nint source, int sourceX, int sourceY, uint rop);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    internal delegate nint SubclassProc(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")] internal static extern bool SetWindowSubclass(nint window, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32.dll")] internal static extern bool RemoveWindowSubclass(nint window, SubclassProc proc, nuint id);
    [DllImport("comctl32.dll")] internal static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);

    internal static List<DisplayInfo> GetDisplays()
    {
        var result = new List<DisplayInfo>();
        EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref Rect bounds, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref info))
                result.Add(new(info.Device, new(info.Monitor.Left, info.Monitor.Top,
                    info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top), (info.Flags & 1) != 0));
            return true;
        }, 0);
        return result.OrderByDescending(d => d.IsPrimary).ToList();
    }

    internal static PixelFrame Capture(CaptureRect region)
    {
        var bytes = checked(region.Width * region.Height * 4);
        if (region.Width <= 0 || region.Height <= 0 || bytes > ScrollStitcher.MaxBytes)
            throw new ArgumentOutOfRangeException(nameof(region));
        var source = GetDC(0);
        var target = CreateCompatibleDC(source);
        nint bitmap = 0, previous = 0;
        try
        {
            if (source == 0 || target == 0) throw new Win32Exception("无法读取桌面。");
            var info = new BitmapInfo { Size = 40, Width = region.Width, Height = -region.Height, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(source, ref info, 0, out var bits, 0, 0);
            if (bitmap == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            previous = SelectObject(target, bitmap);
            if (!BitBlt(target, 0, 0, region.Width, region.Height, source, region.X, region.Y, 0x40CC0020))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var pixels = new byte[bytes];
            Marshal.Copy(bits, pixels, 0, bytes);
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return new(region.Width, region.Height, pixels);
        }
        finally
        {
            if (previous != 0) SelectObject(target, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (target != 0) DeleteDC(target);
            if (source != 0) ReleaseDC(0, source);
        }
    }
}
