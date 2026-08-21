namespace MiniDisplay.Transport;

public static class Msu2Protocol
{
    public const int Width = 160;
    public const int Height = 80;
    public const int FrameBytes = Width * Height * 2;
    public const int SourceChunkBytes = 256;
    public const int EncodedChunkBytes = 390;

    public static ReadOnlyMemory<byte> WakeUp => new byte[] { 0x00, 0x4d, 0x53, 0x4e, 0x43, 0x4e };

    public static byte[] SetArea(int x, int y) =>
        Command(0x02, 0x00, x >> 8, x, y >> 8, y);

    public static byte[] SetSize(int width, int height) =>
        Command(0x02, 0x01, width >> 8, width, height >> 8, height);

    public static byte[] BeginWrite() => Command(0x02, 0x03, 0x07, 0, 0, 0);

    public static byte[] CommitChunk(int validBytes) =>
        validBytes is < 1 or > SourceChunkBytes
            ? throw new ArgumentOutOfRangeException(nameof(validBytes))
            : Command(0x02, 0x03, 0x08, validBytes >> 8, validBytes, 0);

    public static byte[] EncodeChunk(ReadOnlySpan<byte> rgb565BigEndian)
    {
        if (rgb565BigEndian.Length is < 1 or > SourceChunkBytes)
            throw new ArgumentOutOfRangeException(nameof(rgb565BigEndian));

        var result = new byte[EncodedChunkBytes];
        for (int packet = 0; packet < 64; packet++)
        {
            int source = packet * 4;
            int target = packet * 6;
            result[target] = 0x04;
            result[target + 1] = (byte)packet;
            for (int i = 0; i < 4; i++)
                result[target + 2 + i] = source + i < rgb565BigEndian.Length
                    ? rgb565BigEndian[source + i]
                    : (byte)0xff;
        }

        CommitChunk(rgb565BigEndian.Length).CopyTo(result, 384);
        return result;
    }

    public static byte[] SolidColorFrame(ushort rgb565)
    {
        var frame = new byte[FrameBytes];
        for (int i = 0; i < frame.Length; i += 2)
        {
            frame[i] = (byte)(rgb565 >> 8);
            frame[i + 1] = (byte)rgb565;
        }
        return frame;
    }

    private static byte[] Command(byte b0, byte b1, int b2, int b3, int b4, int b5) =>
        new[] { b0, b1, (byte)b2, (byte)b3, (byte)b4, (byte)b5 };
}
