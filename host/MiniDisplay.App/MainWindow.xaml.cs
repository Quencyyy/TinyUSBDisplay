using System.IO;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using MiniDisplay.Imaging;
using MiniDisplay.Transport;
using ImageResizeMode = MiniDisplay.Imaging.ResizeMode;
using ImageRotation = MiniDisplay.Imaging.Rotation;

namespace MiniDisplay.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _keepAlive = new() { Interval = TimeSpan.FromSeconds(2) };
    private BgraImage? _source;
    private byte[]? _frame;
    private Msu2SerialDisplay? _display;
    private bool _sending;

    public MainWindow()
    {
        InitializeComponent();
        PortBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        PortBox.Text = SerialPort.GetPortNames().Contains("COM4") ? "COM4" : SerialPort.GetPortNames().FirstOrDefault() ?? "COM4";
        _keepAlive.Tick += KeepAlive_Tick;
        Closed += async (_, _) => await DisconnectAsync();
    }

    private void SelectImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "圖片|*.png;*.jpg;*.jpeg;*.bmp|所有檔案|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _source = LoadBgra(dialog.FileName);
            FileLabel.Text = Path.GetFileName(dialog.FileName);
            RefreshFrame();
            StatusLabel.Text = "圖片已載入";
        }
        catch (Exception ex) { StatusLabel.Text = $"無法載入圖片：{ex.Message}"; }
    }

    private void Options_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && _source is not null) RefreshFrame();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_frame is null) { StatusLabel.Text = "請先選擇圖片"; return; }
        try
        {
            await DisconnectAsync();
            _display = new Msu2SerialDisplay(PortBox.Text.Trim());
            await _display.ConnectAsync();
            await SendCurrentFrameAsync();
            _keepAlive.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusLabel.Text = $"已連線 {PortBox.Text}，每 2 秒保持畫面";
        }
        catch (Exception ex) { await DisconnectAsync(); StatusLabel.Text = $"連線失敗：{ex.Message}"; }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        await DisconnectAsync();
        StatusLabel.Text = "已停止；裝置將恢復內建動畫";
    }

    private async void KeepAlive_Tick(object? sender, EventArgs e)
    {
        try { await SendCurrentFrameAsync(); }
        catch (Exception ex) { await DisconnectAsync(); StatusLabel.Text = $"傳送中斷：{ex.Message}"; }
    }

    private void RefreshFrame()
    {
        if (_source is not BgraImage source) return;
        ImageResizeMode resize = (ImageResizeMode)Math.Max(0, ResizeBox.SelectedIndex);
        ImageRotation rotation = (ImageRotation)Math.Max(0, RotationBox.SelectedIndex);
        BgraImage transformed = ImageProcessor.Transform(source, Msu2Protocol.Width, Msu2Protocol.Height, resize, rotation);
        _frame = ImageProcessor.ToRgb565BigEndian(transformed);
        PreviewImage.Source = BitmapSource.Create(transformed.Width, transformed.Height, 96, 96,
            PixelFormats.Bgra32, null, transformed.Pixels, transformed.Stride);
    }

    private async Task SendCurrentFrameAsync()
    {
        if (_sending || _display is null || _frame is null) return;
        _sending = true;
        try { await _display.SendFrameAsync(_frame); }
        finally { _sending = false; }
    }

    private async Task DisconnectAsync()
    {
        _keepAlive.Stop();
        if (_display is not null) await _display.DisposeAsync();
        _display = null;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private static BgraImage LoadBgra(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new BgraImage(converted.PixelWidth, converted.PixelHeight, pixels);
    }
}
