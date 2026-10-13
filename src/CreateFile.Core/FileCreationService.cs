using System.Text;
namespace CreateFile.Core;
// Validation is independent of the host OS and always follows Windows filename rules.

public static class FileNameValidator
{
    public static string? Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Enter a filename.";
        if (name.Length > 255) return "Use at most 255 UTF-16 characters.";
        if (name is "." or ".." || name.EndsWith('.') || name.EndsWith(' '))
            return "A filename cannot end with a dot or space.";
        if (name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)))
            return "The filename contains an invalid character or a path separator.";
        var stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789¹²³".Contains(stem[3])))
            return "This name is reserved by Windows.";
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsHighSurrogate(name[i]))
            {
                if (++i == name.Length || !char.IsLowSurrogate(name[i])) return "Invalid Unicode in filename.";
            }
            else if (char.IsLowSurrogate(name[i])) return "Invalid Unicode in filename.";
        }
        return null;
    }
}

public sealed record CreationResult(bool Success, string? Path, string? Error);
public sealed class FileCreationService
{
    public const int MaxTemplateBytes = 1024 * 1024;
    public async Task<CreationResult> CreateAsync(string directory, string filename, string? text = null, CancellationToken cancellationToken = default)
    {
        if (FileNameValidator.Validate(filename) is { } error) return new(false, null, error);
        if (text is not null && Encoding.UTF8.GetByteCount(text) > MaxTemplateBytes)
            return new(false, null, "Templates must be at most 1 MiB of UTF-8 text.");
        try
        {
            if (!Path.IsPathFullyQualified(directory)) return new(false, null, "Choose an absolute folder path.");
            var folder = Path.GetFullPath(directory);
            if (!Directory.Exists(folder)) return new(false, null, "The target folder does not exist or is unavailable.");
            var path = Path.Combine(folder, filename);
            // Atomic exclusive creation: never truncate an existing file, including a concurrent creator's.
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            if (text is not null) await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return new(true, path, null);
        }
        catch (UnauthorizedAccessException) { return new(false, null, "You do not have permission to create a file in this folder."); }
        catch (OperationCanceledException) { return new(false, null, "Creation was cancelled. A partial file may remain; inspect the folder before retrying."); }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 80 or 183) { return new(false, null, "A file or folder with this name already exists. Choose another name."); }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        { return new(false, null, $"Could not create the file: {ex.Message} A partial file may remain if writing began."); }
    }
}
