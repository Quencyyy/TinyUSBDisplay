using System.IO.Ports;

namespace MiniDisplay.Transport;

public sealed class Msu2SerialDisplay : IAsyncDisposable
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _connected;

    public Msu2SerialDisplay(string portName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        _port = new SerialPort(portName, 19_200, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 1_000,
            WriteTimeout = 5_000
        };
    }

    public bool IsConnected => _connected && _port.IsOpen;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;
        _port.Open();
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
        await WriteAsync(Msu2Protocol.WakeUp, cancellationToken);
        await Task.Delay(250, cancellationToken);
        DrainInput();
        _connected = true;
    }

    public async Task SendFrameAsync(ReadOnlyMemory<byte> rgb565BigEndian,
                                     CancellationToken cancellationToken = default)
    {
        if (!IsConnected) throw new InvalidOperationException("Display is not connected.");
        if (rgb565BigEndian.Length != Msu2Protocol.FrameBytes)
            throw new ArgumentException($"Frame must contain {Msu2Protocol.FrameBytes} bytes.", nameof(rgb565BigEndian));

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            DrainInput();
            await WriteAsync(Msu2Protocol.SetArea(0, 0), cancellationToken);
            await WriteAsync(Msu2Protocol.SetSize(Msu2Protocol.Width, Msu2Protocol.Height), cancellationToken);
            byte[] begin = Msu2Protocol.BeginWrite();
            await WriteAsync(begin, cancellationToken);
            await WaitForExactResponseAsync(begin, cancellationToken);

            for (int offset = 0; offset < rgb565BigEndian.Length; offset += Msu2Protocol.SourceChunkBytes)
            {
                int length = Math.Min(Msu2Protocol.SourceChunkBytes, rgb565BigEndian.Length - offset);
                byte[] encoded = Msu2Protocol.EncodeChunk(rgb565BigEndian.Span.Slice(offset, length));
                await WriteAsync(encoded, cancellationToken);
            }
            await WaitForTransmitDrainAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public Task SendSolidColorAsync(ushort rgb565, CancellationToken cancellationToken = default) =>
        SendFrameAsync(Msu2Protocol.SolidColorFrame(rgb565), cancellationToken);

    private Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] buffer = bytes.ToArray();
        _port.Write(buffer, 0, buffer.Length);
        return Task.CompletedTask;
    }

    private async Task WaitForTransmitDrainAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_port.BytesToWrite > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"MSU2 transmit queue did not drain ({_port.BytesToWrite} bytes remain).");
            await Task.Delay(1, cancellationToken);
        }
    }

    private async Task WaitForExactResponseAsync(ReadOnlyMemory<byte> expected,
                                                 CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        int matched = 0;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (_port.BytesToRead > 0)
            {
                byte value = (byte)_port.ReadByte();
                if (value == expected.Span[matched])
                    matched++;
                else
                    matched = value == expected.Span[0] ? 1 : 0;

                if (matched == expected.Length)
                    return;
            }
            await Task.Delay(2, cancellationToken);
        }
        throw new TimeoutException($"MSU2 response timed out after matching {matched}/{expected.Length} bytes.");
    }

    private void DrainInput()
    {
        if (_port.IsOpen && _port.BytesToRead > 0)
            _port.DiscardInBuffer();
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
