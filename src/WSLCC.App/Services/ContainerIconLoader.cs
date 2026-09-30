using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WSLCC.App.Services;

internal static class ContainerIconLoader
{
    public static async Task<ImageSource?> LoadAsync(string path)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            using (var writer = new Windows.Storage.Streams.DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            stream.Seek(0);

            var head = System.Text.Encoding.UTF8.GetString(bytes[..Math.Min(512, bytes.Length)]);
            if (head.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            {
                var svg = new SvgImageSource();
                await svg.SetSourceAsync(stream);
                return svg;
            }
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
