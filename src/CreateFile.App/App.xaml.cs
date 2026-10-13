using Microsoft.UI.Xaml;
using CreateFile.Core;
namespace CreateFile_App;
public partial class App : Application
{
    private Window? window;
    public static ConfigurationStore Store { get; } = new(ConfigurationStore.DefaultPath);
    public static AppSettings Settings { get; private set; } = new();
    internal static void CommitSettings(AppSettings settings) => Settings = settings;
    public App() => InitializeComponent();
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings = await Store.LoadAsync();
        var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (arguments is ["--settings"]) window = new SettingsWindow();
        else
        {
            var folder = arguments is ["--folder", var target] ? Normalize(target) : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            window = new MainWindow(folder, arguments.Length > 0 && arguments is not ["--folder", _] ? "Invalid launch arguments. Expected --folder <path> or --settings." : Store.LoadWarning);
        }
        window.Activate();
    }
    // Classic verbs pass "%V\." so a drive root cannot escape the closing quote; show the resolved folder instead.
    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return path; }
    }
    public static void ApplyTheme(FrameworkElement root) => root.RequestedTheme = Settings.Theme switch
    { AppTheme.Light => ElementTheme.Light, AppTheme.Dark => ElementTheme.Dark, _ => ElementTheme.Default };
}
