using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReSeq.Services;

public sealed class ShellThumbnailService
{
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ShellThumbnailService()
    {
        DefaultThumbnail = CreateDefaultThumbnail();
    }

    public ImageSource DefaultThumbnail { get; }

    public async Task<ImageSource> GetThumbnailAsync(string filePath)
    {
        if (_cache.TryGetValue(filePath, out var cached))
        {
            return cached;
        }

        var thumbnail = await RunStaAsync(() =>
        {
            try
            {
                return GetShellThumbnail(filePath, 180, 100);
            }
            catch
            {
                return null;
            }
        });

        var result = thumbnail ?? DefaultThumbnail;
        _cache[filePath] = result;
        return result;
    }

    private static Task<T?> RunStaAsync<T>(Func<T?> action)
    {
        var completion = new TaskCompletionSource<T?>();
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    private static ImageSource? GetShellThumbnail(string filePath, int width, int height)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(filePath, IntPtr.Zero, iid, out var factory);

        var hBitmap = IntPtr.Zero;
        try
        {
            factory.GetImage(
                new NativeSize(width, height),
                ShellImageFlags.BiggerSizeOk | ShellImageFlags.WideThumbnails | ShellImageFlags.ScaleUp,
                out hBitmap);

            if (hBitmap == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }

            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }

    private static ImageSource CreateDefaultThumbnail()
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(228, 233, 240)),
                null,
                new Rect(0, 0, 180, 100));
            context.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(56, 68, 82)),
                null,
                new Rect(64, 25, 52, 50),
                8,
                8);

            var play = Geometry.Parse("M 84 38 L 84 62 L 104 50 Z");
            context.DrawGeometry(Brushes.White, null, play);
        }

        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory shellItem);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, ShellImageFlags flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public readonly int Width;

        public readonly int Height;
    }

    [Flags]
    private enum ShellImageFlags
    {
        BiggerSizeOk = 0x00000001,
        IconOnly = 0x00000004,
        ThumbnailOnly = 0x00000008,
        WideThumbnails = 0x00000040,
        ScaleUp = 0x00000100
    }
}
