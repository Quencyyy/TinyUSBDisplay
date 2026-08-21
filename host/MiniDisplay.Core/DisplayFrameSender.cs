using MiniDisplay.Protocol;

namespace MiniDisplay.Core;

public interface IDisplayTransport : IAsyncDisposable
{
    Task SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken = default);
}

public sealed class DisplayFrameSender(IDisplayTransport transport)
{
    private byte _frameId;

    public async Task SendSolidColorAsync(ushort rgb565, CancellationToken cancellationToken = default)
    {
        byte frameId = ++_frameId;
        await SendAsync(new Packet(Command.FrameBegin, frameId, 1), cancellationToken);
        var block = new byte[80 * 4 * 2];
        for (int i = 0; i < block.Length; i += 2)
        {
            block[i] = (byte)(rgb565 >> 8);
            block[i + 1] = (byte)rgb565;
        }
        ushort sequence = 0;
        for (byte y = 0; y < 160; y += 4)
            await SendAsync(new Packet(Command.PixelBlock, frameId, 1, 0, y, 80, 4, sequence++, block), cancellationToken);
        await SendAsync(new Packet(Command.FrameEnd, frameId, 1, Sequence: sequence), cancellationToken);
    }

    private Task SendAsync(Packet packet, CancellationToken cancellationToken) =>
        transport.SendAsync(PacketCodec.Encode(packet), cancellationToken);
}

public sealed class PacketFileTransport(Stream output) : IDisplayTransport
{
    public async Task SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken = default)
    {
        byte[] length = BitConverter.GetBytes(packet.Length);
        await output.WriteAsync(length, cancellationToken);
        await output.WriteAsync(packet, cancellationToken);
    }

    public async ValueTask DisposeAsync() => await output.DisposeAsync();
}
