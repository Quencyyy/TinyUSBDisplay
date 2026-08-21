namespace MiniDisplay.Imaging;

public enum ResizeMode { Fit, Fill, Stretch }
public enum Rotation { None, Clockwise90, Clockwise180, Clockwise270 }

public readonly record struct BgraImage(int Width, int Height, byte[] Pixels)
{
    public int Stride => checked(Width * 4);

    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Image dimensions must be positive.");
        if (Pixels is null || Pixels.Length != checked(Width * Height * 4))
            throw new ArgumentException("Pixel buffer must be tightly packed BGRA32.", nameof(Pixels));
    }
}

public static class ImageProcessor
{
    public static BgraImage Transform(BgraImage source, int targetWidth, int targetHeight,
                                      ResizeMode mode, Rotation rotation = Rotation.None)
    {
        source.Validate();
        if (targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetWidth));

        int rotatedWidth = rotation is Rotation.Clockwise90 or Rotation.Clockwise270
            ? source.Height : source.Width;
        int rotatedHeight = rotation is Rotation.Clockwise90 or Rotation.Clockwise270
            ? source.Width : source.Height;

        double scaleX = targetWidth / (double)rotatedWidth;
        double scaleY = targetHeight / (double)rotatedHeight;
        double scale = mode switch
        {
            ResizeMode.Fit => Math.Min(scaleX, scaleY),
            ResizeMode.Fill => Math.Max(scaleX, scaleY),
            _ => 1
        };
        double drawnWidth = mode == ResizeMode.Stretch ? targetWidth : rotatedWidth * scale;
        double drawnHeight = mode == ResizeMode.Stretch ? targetHeight : rotatedHeight * scale;
        double left = (targetWidth - drawnWidth) / 2;
        double top = (targetHeight - drawnHeight) / 2;
        var output = new byte[checked(targetWidth * targetHeight * 4)];

        for (int y = 0; y < targetHeight; y++)
        for (int x = 0; x < targetWidth; x++)
        {
            double rx = (x + 0.5 - left) * rotatedWidth / drawnWidth - 0.5;
            double ry = (y + 0.5 - top) * rotatedHeight / drawnHeight - 0.5;
            if (rx < -0.5 || ry < -0.5 || rx >= rotatedWidth - 0.5 || ry >= rotatedHeight - 0.5)
            {
                output[(y * targetWidth + x) * 4 + 3] = 255;
                continue;
            }

            int rotatedX = Math.Clamp((int)Math.Round(rx), 0, rotatedWidth - 1);
            int rotatedY = Math.Clamp((int)Math.Round(ry), 0, rotatedHeight - 1);
            (int sx, int sy) = MapToSource(rotatedX, rotatedY, source.Width, source.Height, rotation);
            int from = (sy * source.Width + sx) * 4;
            int to = (y * targetWidth + x) * 4;
            output[to] = source.Pixels[from];
            output[to + 1] = source.Pixels[from + 1];
            output[to + 2] = source.Pixels[from + 2];
            output[to + 3] = 255;
        }
        return new BgraImage(targetWidth, targetHeight, output);
    }

    public static byte[] ToRgb565BigEndian(BgraImage image)
    {
        image.Validate();
        var output = new byte[checked(image.Width * image.Height * 2)];
        for (int pixel = 0, target = 0; pixel < image.Pixels.Length; pixel += 4, target += 2)
        {
            int b = image.Pixels[pixel];
            int g = image.Pixels[pixel + 1];
            int r = image.Pixels[pixel + 2];
            ushort rgb565 = (ushort)(((r & 0xf8) << 8) | ((g & 0xfc) << 3) | (b >> 3));
            output[target] = (byte)(rgb565 >> 8);
            output[target + 1] = (byte)rgb565;
        }
        return output;
    }

    private static (int X, int Y) MapToSource(int x, int y, int width, int height, Rotation rotation) =>
        rotation switch
        {
            Rotation.Clockwise90 => (y, height - 1 - x),
            Rotation.Clockwise180 => (width - 1 - x, height - 1 - y),
            Rotation.Clockwise270 => (width - 1 - y, x),
            _ => (x, y)
        };
}
