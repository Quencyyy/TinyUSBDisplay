namespace MiniDisplay.Protocol;

public enum Command : byte
{
    Init = 0x01, Clear = 0x02, FrameBegin = 0x03, PixelBlock = 0x04,
    FrameEnd = 0x05, SetBrightness = 0x06, Ping = 0x07,
    Ack = 0x80, Nack = 0x81, Capabilities = 0x82, Pong = 0x83
}

public sealed record Packet(
    Command Command,
    byte FrameId = 0,
    byte Flags = 0,
    byte X = 0,
    byte Y = 0,
    byte Width = 0,
    byte Height = 0,
    ushort Sequence = 0,
    ReadOnlyMemory<byte> Payload = default);
