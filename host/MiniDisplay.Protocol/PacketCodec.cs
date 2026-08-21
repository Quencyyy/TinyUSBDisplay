using System.Buffers.Binary;

namespace MiniDisplay.Protocol;

public static class PacketCodec
{
    public const int HeaderSize = 16;
    public const int MaxPayload = 640;

    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xffff;
        foreach (byte value in data)
        {
            crc ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    public static byte[] Encode(Packet packet)
    {
        Validate(packet);
        int payloadLength = packet.Payload.Length;
        var result = new byte[HeaderSize + payloadLength + (payloadLength == 0 ? 0 : 2)];
        result[0] = 0x53; result[1] = 0x44; result[2] = 1; result[3] = (byte)packet.Command;
        result[4] = packet.FrameId; result[5] = packet.Flags; result[6] = packet.X; result[7] = packet.Y;
        result[8] = packet.Width; result[9] = packet.Height;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), (ushort)payloadLength);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(12), packet.Sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(14), Crc16(result.AsSpan(0, 14)));
        packet.Payload.Span.CopyTo(result.AsSpan(HeaderSize));
        if (payloadLength != 0)
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(HeaderSize + payloadLength), Crc16(packet.Payload.Span));
        return result;
    }

    public static Packet Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize) throw new InvalidDataException("Incomplete header.");
        if (bytes[0] != 0x53 || bytes[1] != 0x44) throw new InvalidDataException("Invalid magic.");
        if (bytes[2] != 1) throw new InvalidDataException("Unsupported protocol version.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[14..]) != Crc16(bytes[..14]))
            throw new InvalidDataException("Header CRC mismatch.");
        int payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]);
        int total = HeaderSize + payloadLength + (payloadLength == 0 ? 0 : 2);
        if (payloadLength > MaxPayload || bytes.Length != total) throw new InvalidDataException("Invalid packet length.");
        if (payloadLength != 0 && BinaryPrimitives.ReadUInt16LittleEndian(bytes[(HeaderSize + payloadLength)..]) !=
            Crc16(bytes.Slice(HeaderSize, payloadLength))) throw new InvalidDataException("Payload CRC mismatch.");
        var packet = new Packet((Command)bytes[3], bytes[4], bytes[5], bytes[6], bytes[7], bytes[8], bytes[9],
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]), bytes.Slice(HeaderSize, payloadLength).ToArray());
        Validate(packet);
        return packet;
    }

    private static void Validate(Packet packet)
    {
        if (packet.Payload.Length > MaxPayload) throw new ArgumentOutOfRangeException(nameof(packet));
        if (packet.Command != Command.PixelBlock) return;
        if (packet.Width == 0 || packet.Height == 0 || packet.X + packet.Width > 80 || packet.Y + packet.Height > 160)
            throw new ArgumentException("Pixel block is outside the display.", nameof(packet));
        if (packet.Payload.Length != packet.Width * packet.Height * 2)
            throw new ArgumentException("Pixel block payload size does not match its rectangle.", nameof(packet));
    }
}
