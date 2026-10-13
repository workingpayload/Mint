using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
namespace CreateFile_App;
internal static class WindowLayout
{
    public static void Size(Window window, int width, int height)
    {
        var dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
        var scale = dpi / 96.0;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min((int)(width * scale), area.Width), Math.Min((int)(height * scale), area.Height)));
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
}
