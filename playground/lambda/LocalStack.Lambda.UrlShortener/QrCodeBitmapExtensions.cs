using SkiaSharp;
using Net.Codecrete.QrCodeGenerator;

namespace LocalStack.Lambda.UrlShortener;

public static class QrCodeBitmapExtensions
{
    public static SKBitmap ToBitmap(this QrCode qrCode, int scale, int border, SKColor foreground, SKColor background)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Value out of range");
        }

        if (border < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(border), "Value out of range");
        }

        ArgumentNullException.ThrowIfNull(qrCode);

        var size = qrCode.Size;
        var dim = (size + border * 2) * scale;

        if (dim > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale or border too large");
        }

        // create bitmap
        var bitmap = new SKBitmap(dim, dim, SKColorType.Rgb888x, SKAlphaType.Opaque);

        using var canvas = new SKCanvas(bitmap);
        using (var paint = new SKPaint())
        {
            paint.Color = background;
            canvas.DrawRect(0, 0, dim, dim, paint);
        }

        // draw modules
        using (var paint = new SKPaint())
        {
            paint.Color = foreground;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (qrCode.GetModule(x, y))
                    {
                        canvas.DrawRect((x + border) * scale, (y + border) * scale, scale, scale, paint);
                    }
                }
            }
        }

        return bitmap;
    }

    public static SKBitmap ToBitmap(this QrCode qrCode, int scale, int border)
    {
        return qrCode.ToBitmap(scale, border, SKColors.Black, SKColors.White);
    }

    public static byte[] ToPng(this QrCode qrCode, int scale, int border, SKColor foreground, SKColor background)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Value out of range");
        }

        if (border < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(border), "Value out of range");
        }

        ArgumentNullException.ThrowIfNull(qrCode);

        using var bitmap = qrCode.ToBitmap(scale, border, foreground, background);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    public static byte[] ToPng(this QrCode qrCode, int scale, int border)
    {
        return qrCode.ToPng(scale, border, SKColors.Black, SKColors.White);
    }

    public static void SaveAsPng(this QrCode qrCode, string filename, int scale, int border, SKColor foreground, SKColor background)
    {
        if (scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Value out of range");
        }

        if (border < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(border), "Value out of range");
        }

        ArgumentNullException.ThrowIfNull(qrCode);

        using var bitmap = qrCode.ToBitmap(scale, border, foreground, background);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.OpenWrite(filename);
        data.SaveTo(stream);
    }

    public static void SaveAsPng(this QrCode qrCode, string filename, int scale, int border)
    {
        qrCode.SaveAsPng(filename, scale, border, SKColors.Black, SKColors.White);
    }
}
