using System.Text.Json;
using System.Text.Json.Serialization;
namespace CreateFile.Core;

public enum AppTheme { System, Light, Dark }
public sealed record FilePreset(string Name, string Extension, string DefaultFilename, bool Pinned = false)
{
    public override string ToString() => $"{(Pinned ? "★ " : "")}{Name}  {Extension}";
}
public sealed record TextTemplate(string Id, string Name, string Content)
{
    public override string ToString() => Name;
}
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public AppTheme Theme { get; set; }
    public string DefaultFilename { get; set; } = "New file.txt";
    public bool CloseAfterCreation { get; set; } = true;
    public bool OpenAfterCreation { get; set; }
    public bool ShowSuccessNotifications { get; set; } = true;
    public List<FilePreset> Presets { get; set; } = BuiltIns.Presets();
    public List<TextTemplate> Templates { get; set; } = BuiltIns.Templates();
}
public sealed class ConfigurationStore(string path)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CreateFile", "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string? LoadWarning { get; private set; }
    public async Task<AppSettings> LoadAsync()
    {
        LoadWarning = null;
        try
        {
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Configuration exceeds 8 MiB.");
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options) ?? throw new InvalidDataException("Configuration is empty.");
            Validate(settings);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            LoadWarning = $"Settings could not be loaded. Defaults are in use; the original file has not been changed. {ex.Message}";
            return new();
        }
    }
    public async Task SaveAsync(AppSettings settings)
    {
        Validate(settings);
        var json = JsonSerializer.SerializeToUtf8Bytes(settings, Options);
        if (json.Length > 8 * 1024 * 1024) throw new InvalidDataException("Configuration exceeds 8 MiB.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, json);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(AppSettings s)
    {
        if (s.SchemaVersion != 1 || !Enum.IsDefined(s.Theme) || FileNameValidator.Validate(s.DefaultFilename) is not null ||
            s.Presets is null || s.Templates is null || s.Presets.Count > 1000 || s.Templates.Count > 1000)
            throw new InvalidDataException("Invalid or unsupported settings.");
        if (s.Presets.Any(p => p is null || string.IsNullOrWhiteSpace(p.Name) || p.Extension is null ||
            FileNameValidator.Validate(p.DefaultFilename) is not null || (p.Extension.Length > 0 &&
            (!p.Extension.StartsWith('.') || FileNameValidator.Validate("file" + p.Extension) is not null))))
            throw new InvalidDataException("Invalid file preset.");
        foreach (var template in s.Templates) TemplateStore.Validate(template);
        if (s.Templates.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != s.Templates.Count)
            throw new InvalidDataException("Duplicate template IDs.");
    }
}
public static class TemplateStore
{
    public static void Validate(TextTemplate? template)
    {
        if (template is null || string.IsNullOrWhiteSpace(template.Id) || string.IsNullOrWhiteSpace(template.Name) ||
            template.Content is null || System.Text.Encoding.UTF8.GetByteCount(template.Content) > FileCreationService.MaxTemplateBytes)
            throw new InvalidDataException("A template needs an ID, a name, and at most 1 MiB of UTF-8 text.");
    }
    public static async Task<TextTemplate> ImportAsync(string path)
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Template import is too large.");
        var template = JsonSerializer.Deserialize<TextTemplate>(await File.ReadAllTextAsync(path));
        Validate(template);
        return template! with { Id = Guid.NewGuid().ToString("N") };
    }
    public static async Task ExportAsync(string path, TextTemplate template)
    {
        Validate(template);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, template, new JsonSerializerOptions { WriteIndented = true });
    }
}
