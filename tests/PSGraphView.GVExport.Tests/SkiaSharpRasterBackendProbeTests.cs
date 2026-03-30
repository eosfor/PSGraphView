using System.Runtime.InteropServices;
using SkiaSharp;

namespace PSGraphView.GVExport.Tests;

public sealed class SkiaSharpRasterBackendProbeTests
{
    [Fact]
    public void CanCreateSurfaceAndEncodePng()
    {
        var info = new SKImageInfo(16, 12, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info)!;
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            Color = new SKColor(255, 0, 0, 192),
            IsAntialias = false,
        };

        canvas.DrawRect(new SKRect(2, 2, 10, 8), paint);
        canvas.Flush();

        using SKImage image = surface.Snapshot();
        using SKData png = image.Encode(SKEncodedImageFormat.Png, 100);

        Assert.NotNull(png);
        Assert.True(png.Size > 8);

        byte[] pngBytes = png.ToArray();
        Assert.Equal(0x89, pngBytes[0]);
        Assert.Equal((byte)'P', pngBytes[1]);
        Assert.Equal((byte)'N', pngBytes[2]);
        Assert.Equal((byte)'G', pngBytes[3]);
    }

    [Fact]
    public void CanFlattenTransparencyAndEncodeJpeg()
    {
        var info = new SKImageInfo(16, 12, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface sourceSurface = SKSurface.Create(info)!;
        SKCanvas sourceCanvas = sourceSurface.Canvas;
        sourceCanvas.Clear(SKColors.Transparent);

        using (var paint = new SKPaint
        {
            Color = new SKColor(32, 128, 255, 180),
            IsAntialias = false,
        })
        {
            sourceCanvas.DrawRect(new SKRect(3, 2, 13, 10), paint);
        }

        sourceCanvas.Flush();

        using SKImage sourceImage = sourceSurface.Snapshot();
        using SKSurface jpegSurface = SKSurface.Create(info)!;
        SKCanvas jpegCanvas = jpegSurface.Canvas;
        SKColor background = new(255, 255, 254, 255);
        jpegCanvas.Clear(background);
        jpegCanvas.DrawImage(sourceImage, 0, 0);
        jpegCanvas.Flush();

        using SKImage flattened = jpegSurface.Snapshot();
        using SKData jpeg = flattened.Encode(SKEncodedImageFormat.Jpeg, 90);

        Assert.NotNull(jpeg);
        Assert.True(jpeg.Size > 4);

        byte[] jpegBytes = jpeg.ToArray();
        Assert.Equal(0xFF, jpegBytes[0]);
        Assert.Equal(0xD8, jpegBytes[1]);

        using SKData decodedData = SKData.CreateCopy(jpegBytes);
        using SKImage decodedImage = SKImage.FromEncodedData(decodedData)!;

        var readInfo = new SKImageInfo(decodedImage.Width, decodedImage.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(readInfo);
        bool ok = decodedImage.ReadPixels(readInfo, bitmap.GetPixels(), readInfo.RowBytes, 0, 0);
        Assert.True(ok);

        byte[] pixels = new byte[readInfo.RowBytes * readInfo.Height];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

        Assert.Equal(255, pixels[3]);
        Assert.InRange(pixels[0], 240, 255);
        Assert.InRange(pixels[1], 240, 255);
        Assert.InRange(pixels[2], 240, 255);
    }
}
