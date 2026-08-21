using MiniDisplay.Imaging;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// 2x1: red, green (BGRA32)
var source = new BgraImage(2, 1, [0, 0, 255, 255, 0, 255, 0, 255]);
byte[] rgb565 = ImageProcessor.ToRgb565BigEndian(source);
Check(rgb565.SequenceEqual(new byte[] { 0xf8, 0x00, 0x07, 0xe0 }), "RGB565 conversion");

BgraImage stretched = ImageProcessor.Transform(source, 4, 2, ResizeMode.Stretch);
Check(stretched.Width == 4 && stretched.Height == 2, "Stretch dimensions");
Check(stretched.Pixels[2] == 255 && stretched.Pixels[^3] == 255 && stretched.Pixels[^2] == 0, "Stretch colors");

BgraImage fitted = ImageProcessor.Transform(source, 4, 4, ResizeMode.Fit);
Check(fitted.Pixels[3] == 255 && fitted.Pixels[0] == 0 && fitted.Pixels[2] == 0, "Fit letterbox");
Check(fitted.Pixels[(1 * 4) * 4 + 2] == 255, "Fit image placement");

BgraImage rotated = ImageProcessor.Transform(source, 1, 2, ResizeMode.Stretch, Rotation.Clockwise90);
Check(rotated.Pixels[2] == 255 && rotated.Pixels[4 + 1] == 255, "90-degree rotation");

Console.WriteLine("MiniDisplay.Imaging.SelfTest: PASS");
