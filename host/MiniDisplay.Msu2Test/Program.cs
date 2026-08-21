using MiniDisplay.Transport;

string port = args.Length > 0 ? args[0] : "COM4";
int holdSeconds = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 30;

Console.WriteLine($"Connecting to MSU2 on {port}...");
await using var display = new Msu2SerialDisplay(port);
await display.ConnectAsync();
byte[] redFrame = Msu2Protocol.SolidColorFrame(0xf800);
Console.WriteLine($"Sending a red refresh every 2 seconds for {holdSeconds} seconds.");
var deadline = DateTime.UtcNow.AddSeconds(holdSeconds);
int refreshes = 0;
while (DateTime.UtcNow < deadline)
{
    await display.SendFrameAsync(redFrame);
    Console.WriteLine($"Refresh {++refreshes}: {DateTime.Now:HH:mm:ss}");
    TimeSpan remaining = deadline - DateTime.UtcNow;
    if (remaining > TimeSpan.Zero)
        await Task.Delay(remaining < TimeSpan.FromSeconds(2) ? remaining : TimeSpan.FromSeconds(2));
}
Console.WriteLine("Disconnecting; the built-in animation may resume.");
