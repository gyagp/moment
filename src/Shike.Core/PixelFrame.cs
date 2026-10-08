namespace Shike.Core;

/// <summary>A top-down, tightly packed BGRA32 image in physical pixels.</summary>
public sealed record PixelFrame
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public int Stride => checked(Width * 4);

    public PixelFrame(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != checked(width * height * 4))
            throw new ArgumentException("Pixel buffer does not match the image dimensions.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public PixelFrame Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Width || y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(width));
        var pixels = new byte[checked(width * height * 4)];
        for (var row = 0; row < height; row++)
            Buffer.BlockCopy(Pixels, (y + row) * Stride + x * 4, pixels, row * width * 4, width * 4);
        return new(width, height, pixels);
    }
}
