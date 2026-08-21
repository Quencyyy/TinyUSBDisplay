using MiniDisplay.Protocol;

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}

Check(PacketCodec.Crc16("123456789"u8) == 0x29b1, "CRC check vector failed");
var pixels = new byte[160];
for (int i = 0; i < pixels.Length; i += 2) { pixels[i] = 0xf8; pixels[i + 1] = 0x00; }
var source = new Packet(Command.PixelBlock, 7, 1, 0, 42, 80, 1, 12, pixels);
byte[] encoded = PacketCodec.Encode(source);
Packet decoded = PacketCodec.Decode(encoded);
Check(encoded.Length == 178, "Encoded size failed");
Check(decoded.Command == source.Command && decoded.FrameId == source.FrameId &&
    decoded.Flags == source.Flags && decoded.X == source.X && decoded.Y == source.Y &&
    decoded.Width == source.Width && decoded.Height == source.Height &&
    decoded.Sequence == source.Sequence, "Packet metadata round trip failed");
Check(decoded.Payload.Span.SequenceEqual(pixels), "Payload round trip failed");
encoded[16] ^= 1;
try { PacketCodec.Decode(encoded); throw new Exception("Bad payload CRC accepted"); }
catch (InvalidDataException) { }
Console.WriteLine("MiniDisplay.Protocol.SelfTest: PASS");
