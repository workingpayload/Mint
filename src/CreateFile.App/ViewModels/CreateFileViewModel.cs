using System.ComponentModel;
using System.Runtime.CompilerServices;
using CreateFile.Core;
namespace CreateFile_App.ViewModels;

public sealed class CreateFileViewModel(AppSettings settings, string directory) : INotifyPropertyChanged
{
    private string filename = settings.DefaultFilename;
    private string message = "";
    private bool busy;
    private string directoryPath = directory;
    public string Directory { get => directoryPath; set { directoryPath = value; Changed(); } }
    public string Filename { get => filename; set { filename = value; Message = FileNameValidator.Validate(value) ?? ""; Changed(); Changed(nameof(CanCreate)); } }
    public string Message { get => message; set { message = value; Changed(); } }
    public bool Busy { get => busy; private set { busy = value; Changed(); Changed(nameof(CanCreate)); } }
    public bool CanCreate => !Busy && FileNameValidator.Validate(Filename) is null;
    public async Task<CreationResult> CreateAsync(string? content)
    {
        Busy = true;
        try
        {
            var target = Directory;
            var name = Filename;
            // Opening a file and checking a UNC directory can block before the first await.
            // Keep even that work off the WinUI dispatcher.
            var result = await Task.Run(() => new FileCreationService().CreateAsync(target, name, content));
            Message = result.Success ? "File created." : result.Error ?? "Creation failed.";
            return result;
        }
        finally { Busy = false; }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
