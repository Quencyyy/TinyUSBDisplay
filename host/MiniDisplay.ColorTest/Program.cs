using MiniDisplay.Core;

string outputPath = args.Length == 0 ? "color-test.packets" : args[0];
await using var file = File.Create(outputPath);
await using var transport = new PacketFileTransport(file);
var sender = new DisplayFrameSender(transport);

var colors = new (string Name, ushort Rgb565)[]
{
    ("Red", 0xf800), ("Green", 0x07e0), ("Blue", 0x001f),
    ("White", 0xffff), ("Black", 0x0000)
};
foreach (var color in colors)
{
    await sender.SendSolidColorAsync(color.Rgb565);
    Console.WriteLine($"Encoded {color.Name}");
}
await file.FlushAsync();
Console.WriteLine($"Wrote {new FileInfo(outputPath).Length:N0} bytes to {Path.GetFullPath(outputPath)}");
