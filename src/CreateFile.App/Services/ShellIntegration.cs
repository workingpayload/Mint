using Microsoft.Win32;
using System.Runtime.InteropServices;
namespace CreateFile_App.Services;

public static class ShellIntegration
{
    private const string Verb = "CreateFile.New";
    private static readonly string[] Locations = [@"Directory\Background", "Directory", "DesktopBackground"];
    public static bool ClassicEnabled => Locations.All(p => { using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{p}\shell\{Verb}\command"); return key is not null; });
    public static void EnableClassic()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the application.");
        // A literal \. suffix prevents a trailing separator from escaping the closing quote.
        foreach (var location in Locations)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{location}\shell\{Verb}");
            key.SetValue("", "Create New File");
            key.SetValue("Icon", $"\"{executable}\",0");
            using var command = key.CreateSubKey("command");
            command.SetValue("", $"\"{executable}\" --folder \"{(location == "Directory" ? "%1" : "%V")}\\.\"");
        }
        NotifyAssociations();
    }
    public static void RemoveClassic()
    {
        foreach (var location in Locations) Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{location}\shell\{Verb}", false);
        NotifyAssociations();
    }
    public static string ModernStatus()
    {
        try
        {
            var package = Windows.ApplicationModel.Package.Current;
            return $"Package registered: {package.Id.Name}. Modern menu registration is supplied by the MSIX manifest. Explorer visibility must be verified; reinstall after shell changes.";
        }
        catch (InvalidOperationException) { return "Unpackaged app. Install the MSIX package to register the modern Windows 11 command."; }
    }
    public static void NotifyCreated(string path) => SHChangeNotify(0x00000002, 0x0005, path, IntPtr.Zero);
    private static void NotifyAssociations() => SHChangeNotify(0x08000000, 0, null, IntPtr.Zero);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, string? item1, IntPtr item2);
}
