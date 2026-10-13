using CreateFile.Core;
using CreateFile.Testing;
using System.Security.AccessControl;
using System.Security.Principal;
namespace CreateFile.Core.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "CreateFileTests-" + Guid.NewGuid().ToString("N"));
    public CoreTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, true);
    [Theory]
    [InlineData("main.py")][InlineData("file.customextension")][InlineData("Dockerfile")]
    [InlineData(".env")][InlineData(".gitignore")][InlineData("你好-नमस्ते-🙂.txt")]
    public async Task CreatesValidNames(string name)
    {
        var result = await new FileCreationService().CreateAsync(folder, name);
        Assert.True(result.Success, result.Error);
        Assert.Equal(0, new FileInfo(result.Path!).Length);
    }
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData(".")][InlineData("..")][InlineData("../escape.txt")]
    [InlineData("a/b")][InlineData("a\\b")][InlineData("a:b")][InlineData("a*b")]
    [InlineData("a?b")][InlineData("a\"b")][InlineData("a<b")][InlineData("a>b")]
    [InlineData("a|b")][InlineData("a\nb")][InlineData("file.")][InlineData("file ")]
    [InlineData("CON")][InlineData("nul.txt")][InlineData("PRN")][InlineData("AUX.json")]
    [InlineData("COM1")][InlineData("LPT9.log")][InlineData("COM¹.txt")][InlineData("LPT²")]
    [InlineData("CON .txt")][InlineData("CONIN$")][InlineData("CONOUT$")]
    public async Task RejectsInvalidNames(string name)
    {
        Assert.NotNull(FileNameValidator.Validate(name));
        Assert.False((await new FileCreationService().CreateAsync(folder, name)).Success);
        Assert.Empty(Directory.GetFiles(folder));
    }
    [Fact] public void RejectsOversizedName() => Assert.NotNull(FileNameValidator.Validate(new string('x', 256)));
    [Fact] public async Task CollisionPreservesContent()
    {
        var service = new FileCreationService();
        Assert.True((await service.CreateAsync(folder, "file.txt", "original")).Success);
        Assert.False((await service.CreateAsync(folder, "file.txt", "replacement")).Success);
        Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(folder, "file.txt")));
    }
    [Fact] public async Task ConcurrentCreatorsHaveExactlyOneWinner()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => new FileCreationService().CreateAsync(folder, "race.txt", i.ToString())));
        Assert.Single(results.Where(r => r.Success));
    }
    [Fact] public async Task RejectsMissingFolder() =>
        Assert.False((await new FileCreationService().CreateAsync(Path.Combine(folder, "missing"), "file")).Success);
    [Fact] public async Task RejectsRelativeFolder() =>
        Assert.False((await new FileCreationService().CreateAsync(".", "file")).Success);
    [Fact] public async Task FolderCollisionIsNotOverwritten()
    {
        Directory.CreateDirectory(Path.Combine(folder, "existing"));
        Assert.False((await new FileCreationService().CreateAsync(folder, "existing")).Success);
    }
    [Fact] public async Task ReadOnlyFolderReportsPermissionError()
    {
        var info = new DirectoryInfo(folder);
        var acl = info.GetAccessControl(AccessControlSections.Access);
        var original = acl.GetSecurityDescriptorBinaryForm();
        acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
            FileSystemRights.CreateFiles, AccessControlType.Deny));
        try { info.SetAccessControl(acl); }
        catch (UnauthorizedAccessException) { throw new SkipException("The test host denies changing ACLs; run this case outside the sandbox."); }
        try
        {
            var result = await new FileCreationService().CreateAsync(folder, "denied.txt");
            Assert.False(result.Success);
            Assert.Contains("permission", result.Error);
        }
        finally
        {
            var restored = new DirectorySecurity();
            restored.SetSecurityDescriptorBinaryForm(original, AccessControlSections.Access);
            info.SetAccessControl(restored);
        }
    }
    [Fact] public async Task TemplateIsLiteralUtf8WithoutBom()
    {
        const string content = "你好\n$(do-not-run)\n";
        var result = await new FileCreationService().CreateAsync(folder, "template.txt", content);
        Assert.True(result.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(content), await File.ReadAllBytesAsync(result.Path!));
    }
    [Fact] public async Task OversizedTemplateDoesNotCreateFile()
    {
        Assert.False((await new FileCreationService().CreateAsync(folder, "large", new string('x', FileCreationService.MaxTemplateBytes + 1))).Success);
        Assert.Empty(Directory.GetFiles(folder));
    }
    [Fact] public async Task ConfigurationRoundTrips()
    {
        var store = new ConfigurationStore(Path.Combine(folder, "settings.json"));
        var settings = new AppSettings { Theme = AppTheme.Dark, DefaultFilename = ".env", CloseAfterCreation = false };
        settings.Presets.Reverse();
        settings.Templates.Add(new("custom", "Custom", "hello"));
        await store.SaveAsync(settings);
        var actual = await store.LoadAsync();
        Assert.Equal(settings.Theme, actual.Theme);
        Assert.Equal(settings.DefaultFilename, actual.DefaultFilename);
        Assert.Equal(settings.Presets, actual.Presets);
        Assert.Equal(settings.Templates, actual.Templates);
        Assert.False(actual.CloseAfterCreation);
    }
    [Fact] public async Task CorruptConfigurationIsPreserved()
    {
        var path = Path.Combine(folder, "settings.json");
        await File.WriteAllTextAsync(path, "{invalid");
        var store = new ConfigurationStore(path);
        Assert.NotEmpty((await store.LoadAsync()).Presets);
        Assert.NotNull(store.LoadWarning);
        Assert.Equal("{invalid", await File.ReadAllTextAsync(path));
    }
    [Fact] public async Task TemplateImportExport()
    {
        var path = Path.Combine(folder, "template.json");
        var original = BuiltIns.Templates()[0];
        await TemplateStore.ExportAsync(path, original);
        var imported = await TemplateStore.ImportAsync(path);
        Assert.Equal(original.Content, imported.Content);
        Assert.NotEqual(original.Id, imported.Id);
        await Assert.ThrowsAsync<IOException>(() => TemplateStore.ExportAsync(path, original));
    }
    [Fact] public async Task AllBuiltInPresetsCreate()
    {
        foreach (var p in BuiltIns.Presets())
        {
            var target = Directory.CreateDirectory(Path.Combine(folder, Guid.NewGuid().ToString("N"))).FullName;
            var result = await new FileCreationService().CreateAsync(target, p.DefaultFilename);
            Assert.True(result.Success, result.Error);
        }
    }
    [Fact] public async Task AllBuiltInTemplatesCreate()
    {
        foreach (var t in BuiltIns.Templates())
        {
            var result = await new FileCreationService().CreateAsync(folder, t.Id + ".txt", t.Content);
            Assert.True(result.Success, result.Error);
            Assert.Equal(t.Content, await File.ReadAllTextAsync(result.Path!));
        }
    }
    [Fact] public async Task InvalidConfigurationDoesNotReplaceGoodConfiguration()
    {
        var store = new ConfigurationStore(Path.Combine(folder, "settings.json"));
        await store.SaveAsync(new AppSettings());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new AppSettings { DefaultFilename = "../bad" }));
        Assert.Equal("New file.txt", (await store.LoadAsync()).DefaultFilename);
    }
    [Fact] public async Task MalformedTemplateImportIsRejected()
    {
        var path = Path.Combine(folder, "bad.json");
        await File.WriteAllTextAsync(path, "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => TemplateStore.ImportAsync(path));
    }
    [Fact] public async Task UnsupportedConfigurationVersionIsPreserved()
    {
        var path = Path.Combine(folder, "settings.json");
        const string content = "{\"SchemaVersion\":999}";
        await File.WriteAllTextAsync(path, content);
        var store = new ConfigurationStore(path);
        await store.LoadAsync();
        Assert.NotNull(store.LoadWarning);
        Assert.Equal(content, await File.ReadAllTextAsync(path));
    }
    [Theory]
    [InlineData("python", "Python")][InlineData("PY", "Python")][InlineData(".py", "Python")]
    [InlineData("gitignore", "Git ignore")][InlineData(".gitignore", "Git ignore")][InlineData("env", "Environment")]
    [InlineData("docker", "Dockerfile")][InlineData("c++", "C++")][InlineData("c#", "C#")]
    [InlineData("cs", "C#")][InlineData("readme", "Markdown")][InlineData(" Plain  Text ", "Plain text")]
    public void PresetSearchFindsBestMatchFirst(string query, string expected) =>
        Assert.Equal(expected, PresetSearch.Find(BuiltIns.Presets(), query).First().Name);
    [Fact] public void PresetSearchEmptyQueryListsAllFavoritesFirst()
    {
        var all = BuiltIns.Presets();
        var results = PresetSearch.Find(all, "  ").ToList();
        Assert.Equal(all.Count, results.Count);
        Assert.True(results.TakeWhile(p => p.Pinned).Count() == all.Count(p => p.Pinned), "Favorites must come first.");
    }
    [Fact] public void PresetSearchWithoutMatchesIsEmpty() => Assert.Empty(PresetSearch.Find(BuiltIns.Presets(), "zzz-no-such-type"));
}

