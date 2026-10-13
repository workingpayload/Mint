using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using CreateFile.Core;
using CreateFile_App.Services;
using CreateFile_App.ViewModels;
using Windows.System;
namespace CreateFile_App;
public sealed partial class MainWindow : Window
{
    private readonly CreateFileViewModel model;
    private SettingsWindow? settingsWindow;
    private bool dismissingSuggestions;
    public MainWindow(string folder, string? warning = null)
    {
        InitializeComponent();
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        // Escape first dismisses open search suggestions instead of closing the window.
        Root.PreviewKeyDown += (_, e) => dismissingSuggestions = e.Key == VirtualKey.Escape && SearchBox.IsSuggestionListOpen;
        WindowLayout.Size(this, 600, 760);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        model = new(App.Settings, folder);
        Root.DataContext = model;
        App.ApplyTheme(Root);
        RefreshChoices();
        if (warning is not null) model.Message = warning;
        Root.Loaded += (_, _) => { FilenameBox.Focus(FocusState.Programmatic); FilenameBox.SelectAll(); };
    }
    private void RefreshChoices()
    {
        Presets.ItemsSource = PresetSearch.Find(App.Settings.Presets, null).ToList();
        var templateId = (Templates.SelectedItem as TextTemplate)?.Id;
        var templates = new[] { new TextTemplate("", "Empty file", "") }.Concat(App.Settings.Templates).ToList();
        Templates.ItemsSource = templates;
        Templates.SelectedIndex = Math.Max(0, templates.FindIndex(t => t.Id == templateId));
    }
    private void SearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        if (string.IsNullOrWhiteSpace(sender.Text)) { sender.ItemsSource = null; return; }
        var matches = PresetSearch.Find(App.Settings.Presets, sender.Text).Cast<object>().ToList();
        sender.ItemsSource = matches.Count > 0 ? matches : new List<object> { "No matching file types" };
    }
    private void SearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // Enter without picking a suggestion applies the best match.
        var preset = args.ChosenSuggestion as FilePreset ?? (args.ChosenSuggestion is null ? PresetSearch.Find(App.Settings.Presets, args.QueryText).FirstOrDefault() : null);
        if (preset is null) return;
        sender.Text = ""; sender.ItemsSource = null;
        Presets.SelectedItem = preset;
        model.Filename = preset.DefaultFilename;
        FilenameBox.Focus(FocusState.Programmatic);
        var stem = preset.DefaultFilename.LastIndexOf('.');
        FilenameBox.Select(0, stem > 0 ? stem : preset.DefaultFilename.Length);
    }
    private void PresetChanged(object sender, SelectionChangedEventArgs e)
    { if (Presets.SelectedItem is FilePreset preset) model.Filename = preset.DefaultFilename; }
    private async void CreateClicked(object sender, RoutedEventArgs e) => await CreateAsync();
    private async Task CreateAsync()
    {
        if (!model.CanCreate) return;
        SuccessBar.IsOpen = false;
        var template = Templates.SelectedItem as TextTemplate;
        var result = await model.CreateAsync(template?.Id == "" ? null : template?.Content);
        if (!result.Success) return;
        ShellIntegration.NotifyCreated(result.Path!);
        model.Message = "";
        if (App.Settings.ShowSuccessNotifications) { SuccessBar.Message = result.Path; SuccessBar.IsOpen = true; }
        if (App.Settings.OpenAfterCreation)
        {
            // Open as text, never execute a script through its file association.
            try
            {
                var process = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe")) { UseShellExecute = false };
                process.ArgumentList.Add(result.Path!);
                System.Diagnostics.Process.Start(process)?.Dispose();
            }
            catch (Exception ex) { model.Message = $"File created, but the editor could not open: {ex.Message}"; return; }
        }
        if (App.Settings.CloseAfterCreation) Close();
    }
    private async void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && dismissingSuggestions) { e.Handled = true; }
        else if (e.Key == VirtualKey.Escape && !model.Busy) { e.Handled = true; Close(); }
        else if (e.Key == VirtualKey.Enter && IsInside(e.OriginalSource, FilenameBox)) { e.Handled = true; await CreateAsync(); }
    }
    private static bool IsInside(object? source, DependencyObject target)
    {
        for (var element = source as DependencyObject; element is not null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            if (ReferenceEquals(element, target)) return true;
        return false;
    }
    private void Cancel(object sender, RoutedEventArgs e) { if (!model.Busy) Close(); }
    private async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        if (model.Busy) return;
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker(); picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null) model.Directory = folder.Path;
        }
        catch (Exception ex) { model.Message = $"Could not select the folder: {ex.Message}"; }
    }
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        if (settingsWindow is null)
        {
            settingsWindow = new SettingsWindow();
            settingsWindow.Closed += (_, _) => { settingsWindow = null; RefreshChoices(); App.ApplyTheme(Root); };
        }
        settingsWindow.Activate();
    }
}
