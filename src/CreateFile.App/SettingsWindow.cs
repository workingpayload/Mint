using CreateFile.Core;
using CreateFile_App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Windows.Storage.Pickers;
namespace CreateFile_App;

/// <summary>Settings edits are committed explicitly; the shared model is persisted atomically.</summary>
public sealed class SettingsWindow : Window
{
    private readonly StackPanel panel = new() { Spacing = 16, Padding = new Thickness(28), MaxWidth = 820 };
    private readonly InfoBar status = new() { IsClosable = true };
    private readonly AppSettings settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(App.Settings))!;
    private readonly ListView presets = new() { Height = 200, SelectionMode = ListViewSelectionMode.Single };
    private readonly ListView templates = new() { Height = 180, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox presetName = Field("Type name"), extension = Field("Extension (for example .py, or leave empty)"), defaultName = Field("Default filename");
    private readonly TextBox templateName = Field("Template name"), templateContent = Field("Template text");
    private readonly CheckBox pinned = new() { Content = "Pin to favorites" };
    private readonly TextBox defaultFilename = Field("Default filename");
    private readonly ComboBox theme = new() { Header = "Theme", ItemsSource = Enum.GetValues<AppTheme>() };
    private readonly ToggleSwitch close = new() { Header = "Close after creation" }, open = new() { Header = "Open created file in Notepad (never execute it)" }, notify = new() { Header = "Show success message in the dialog" };

    public SettingsWindow()
    {
        Title = "Mint for Explorer · Settings";
        SystemBackdrop = new MicaBackdrop();
        WindowLayout.Size(this, 840, 820);
        Content = new ScrollViewer { Content = panel };
        App.ApplyTheme(panel);
        panel.Children.Add(new TextBlock { Text = "Settings", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(status);
        Section("General");
        theme.SelectedItem = settings.Theme;
        defaultFilename.Text = settings.DefaultFilename;
        close.IsOn = settings.CloseAfterCreation; open.IsOn = settings.OpenAfterCreation; notify.IsOn = settings.ShowSuccessNotifications;
        foreach (var control in new UIElement[] { theme, defaultFilename, close, open, notify }) panel.Children.Add(control);
        panel.Children.Add(Button("Save general settings", async () =>
        {
            if (FileNameValidator.Validate(defaultFilename.Text) is { } error) throw new InvalidDataException(error);
            settings.Theme = (AppTheme)theme.SelectedItem; settings.DefaultFilename = defaultFilename.Text;
            settings.CloseAfterCreation = close.IsOn; settings.OpenAfterCreation = open.IsOn; settings.ShowSuccessNotifications = notify.IsOn;
            await Save(); App.ApplyTheme(panel);
        }));
        Section("Context menu");
        panel.Children.Add(Paragraph(ShellIntegration.ModernStatus()));
        panel.Children.Add(Paragraph("Classic integration is optional. Packaged commands may also appear in the classic menu; leave classic disabled if that would create a duplicate. Modern integration is removed by uninstalling the package."));
        var classic = new TextBlock { Text = $"Classic integration: {(ShellIntegration.ClassicEnabled ? "enabled" : "disabled")}" };
        panel.Children.Add(classic);
        panel.Children.Add(Row(Button("Enable / repair classic", () => { ShellIntegration.EnableClassic(); classic.Text = "Classic integration: enabled"; return Task.CompletedTask; }),
            Button("Remove classic", () => { ShellIntegration.RemoveClassic(); classic.Text = "Classic integration: disabled"; return Task.CompletedTask; })));
        panel.Children.Add(Button("Modern integration help", () => { Show("Install or reinstall the signed MSIX, then open a new Explorer window. Use scripts/Verify-Integration.ps1 for registration diagnostics. Uninstall the package to remove its modern command."); return Task.CompletedTask; }));
        Section("File types and favorites");
        panel.Children.Add(presets);
        presets.SelectionChanged += (_, _) => { if (presets.SelectedItem is FilePreset p) { presetName.Text = p.Name; extension.Text = p.Extension; defaultName.Text = p.DefaultFilename; pinned.IsChecked = p.Pinned; } };
        foreach (var control in new UIElement[] { presetName, extension, defaultName, pinned }) panel.Children.Add(control);
        panel.Children.Add(Row(Button("Add", () => ChangePreset(false)), Button("Update selected", () => ChangePreset(true)), Button("Delete", async () => { if (presets.SelectedItem is FilePreset p) { settings.Presets.Remove(p); await Save(); Refresh(); } })));
        panel.Children.Add(Row(Button("Move up", () => MovePreset(-1)), Button("Move down", () => MovePreset(1))));
        Section("Text templates");
        panel.Children.Add(Paragraph("Templates are stored as literal UTF-8 text and are never executed. Empty file is always available in the creation dialog."));
        panel.Children.Add(templates);
        templateContent.AcceptsReturn = true; templateContent.TextWrapping = TextWrapping.Wrap; templateContent.Height = 220;
        templates.SelectionChanged += (_, _) => { if (templates.SelectedItem is TextTemplate t) { templateName.Text = t.Name; templateContent.Text = t.Content; } };
        panel.Children.Add(templateName); panel.Children.Add(templateContent);
        panel.Children.Add(Row(Button("Add", () => ChangeTemplate(false)), Button("Update selected", () => ChangeTemplate(true)), Button("Delete", async () => { if (templates.SelectedItem is TextTemplate t) { settings.Templates.Remove(t); await Save(); Refresh(); } })));
        panel.Children.Add(Row(Button("Import JSON…", ImportTemplate), Button("Export selected…", ExportTemplate)));
        Section("About");
        panel.Children.Add(Paragraph($"Mint for Explorer {typeof(App).Assembly.GetName().Version} · MIT License\nCopyright © 2026 Mint contributors.\nNo background service, startup task, or telemetry."));
        panel.Children.Add(Paragraph($"Source code and releases: {ProjectInfo.RepositoryUrl}"));
        panel.Children.Add(Row(Button("GitHub repository", () => OpenProject(false)), Button("Check for updates", () => OpenProject(true))));
        Refresh();
        if (App.Store.LoadWarning is { } warning) Show(warning, true);
    }
    private async Task ChangePreset(bool update)
    {
        if (string.IsNullOrWhiteSpace(presetName.Text)) throw new InvalidDataException("Enter a type name.");
        if (FileNameValidator.Validate(defaultName.Text) is { } error) throw new InvalidDataException(error);
        if (extension.Text.Length > 0 && (!extension.Text.StartsWith('.') || FileNameValidator.Validate("file" + extension.Text) is not null)) throw new InvalidDataException("Enter a valid extension beginning with a dot.");
        var p = new FilePreset(presetName.Text, extension.Text, defaultName.Text, pinned.IsChecked == true);
        if (update) { if (presets.SelectedIndex < 0) throw new InvalidDataException("Select a preset first."); settings.Presets[presets.SelectedIndex] = p; }
        else settings.Presets.Add(p);
        await Save(); Refresh();
    }
    private async Task MovePreset(int delta)
    {
        var index = presets.SelectedIndex; var target = index + delta;
        if (index < 0 || target < 0 || target >= settings.Presets.Count) return;
        (settings.Presets[index], settings.Presets[target]) = (settings.Presets[target], settings.Presets[index]);
        await Save(); Refresh(); presets.SelectedIndex = target;
    }
    private async Task ChangeTemplate(bool update)
    {
        var old = templates.SelectedItem as TextTemplate;
        if (update && old is null) throw new InvalidDataException("Select a template first.");
        var value = new TextTemplate(update ? old!.Id : Guid.NewGuid().ToString("N"), templateName.Text, templateContent.Text);
        TemplateStore.Validate(value);
        if (update) settings.Templates[templates.SelectedIndex] = value; else settings.Templates.Add(value);
        await Save(); Refresh();
    }
    private async Task ImportTemplate()
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        settings.Templates.Add(await TemplateStore.ImportAsync(file.Path)); await Save(); Refresh();
    }
    private async Task ExportTemplate()
    {
        if (templates.SelectedItem is not TextTemplate t) throw new InvalidDataException("Select a template first.");
        var picker = new FileSavePicker { SuggestedFileName = "template" }; picker.FileTypeChoices.Add("JSON template", new List<string> { ".json" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync(); if (file is null) return;
        // The save picker explicitly confirms replacement and creates the chosen output file.
        await Windows.Storage.FileIO.WriteTextAsync(file, System.Text.Json.JsonSerializer.Serialize(t, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Show("Template exported.");
    }
    private async Task OpenProject(bool releases)
    {
        if (string.IsNullOrWhiteSpace(ProjectInfo.RepositoryUrl)) { Show("Repository URL is not configured for this build.", true); return; }
        await Windows.System.Launcher.LaunchUriAsync(new Uri(ProjectInfo.RepositoryUrl + (releases ? "/releases/latest" : "")));
    }
    private async Task Save()
    {
        await App.Store.SaveAsync(settings);
        // Publish a snapshot only after durable persistence succeeds; failed edits stay in this window.
        App.CommitSettings(System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!);
        Show("Saved.");
    }
    private void Refresh() { presets.ItemsSource = settings.Presets.ToList(); templates.ItemsSource = settings.Templates.ToList(); }
    private void Show(string message, bool error = false) { status.Message = message; status.Severity = error ? InfoBarSeverity.Error : InfoBarSeverity.Success; status.IsOpen = true; }
    private void Section(string title) => panel.Children.Add(new TextBlock { Text = title, FontSize = 20, Margin = new Thickness(0, 16, 0, 0), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
    private static TextBox Field(string label) { var field = new TextBox { Header = label }; AutomationProperties.SetName(field, label); return field; }
    private static TextBlock Paragraph(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static StackPanel Row(params UIElement[] controls) { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; foreach (var c in controls) row.Children.Add(c); return row; }
    private Button Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label };
        button.Click += async (_, _) => { button.IsEnabled = false; try { await action(); } catch (Exception ex) { Show(ex.Message, true); } finally { button.IsEnabled = true; } };
        return button;
    }
}
public static class ProjectInfo { public const string RepositoryUrl = "https://github.com/workingpayload/Mint"; }
