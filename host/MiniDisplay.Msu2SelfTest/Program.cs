using MiniDisplay.Transport;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}

Check(Msu2Protocol.WakeUp.Span.SequenceEqual(new byte[] { 0, 0x4d, 0x53, 0x4e, 0x43, 0x4e }), "Wake-up vector");
Check(Msu2Protocol.SetSize(160, 80).SequenceEqual(new byte[] { 2, 1, 0, 160, 0, 80 }), "Size command");
byte[] red = Msu2Protocol.SolidColorFrame(0xf800);
Check(red.Length == 25_600 && red[0] == 0xf8 && red[1] == 0, "Solid red frame");
byte[] chunk = Msu2Protocol.EncodeChunk(red.AsSpan(0, 256));
Check(chunk.Length == 390, "Encoded chunk length");
Check(chunk.AsSpan(0, 8).SequenceEqual(new byte[] { 4, 0, 0xf8, 0, 0xf8, 0, 4, 1 }), "Chunk framing");
Check(chunk.AsSpan(384).SequenceEqual(new byte[] { 2, 3, 8, 1, 0, 0 }), "Chunk commit");
Console.WriteLine("MiniDisplay.Msu2SelfTest: PASS");
