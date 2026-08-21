using System.Diagnostics;
using MiniDisplay.Transport;

string port = args.Length > 0 ? args[0] : "COM4";
int secondsPerStage = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 3;
int[] targets = { 5, 8, 10, 12 };
ushort[] colors = { 0xf800, 0x07e0, 0x001f, 0xffff, 0x0000, 0xffe0, 0x07ff, 0xf81f };
byte[][] frames = colors.Select(Msu2Protocol.SolidColorFrame).ToArray();

Console.WriteLine($"Connecting to {port}; each FPS stage lasts {secondsPerStage} seconds.");
await using var display = new Msu2SerialDisplay(port);
await display.ConnectAsync();

int colorIndex = 0;
foreach (int targetFps in targets)
{
    TimeSpan interval = TimeSpan.FromSeconds(1d / targetFps);
    var stage = Stopwatch.StartNew();
    TimeSpan nextFrame = TimeSpan.Zero;
    int sent = 0;
    int late = 0;

    Console.WriteLine($"Stage {targetFps,2} FPS started...");
    while (stage.Elapsed < TimeSpan.FromSeconds(secondsPerStage))
    {
        await display.SendFrameAsync(frames[colorIndex++ % frames.Length]);
        sent++;
        nextFrame += interval;
        TimeSpan wait = nextFrame - stage.Elapsed;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait);
        else
            late++;
    }

    stage.Stop();
    double actual = sent / stage.Elapsed.TotalSeconds;
    Console.WriteLine($"Stage {targetFps,2} FPS: actual={actual:F2}, frames={sent}, late={late}");
}

Console.WriteLine("Benchmark complete; disconnecting and restoring the built-in animation.");
