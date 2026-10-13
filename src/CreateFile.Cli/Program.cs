using CreateFile.Core;
if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Mint CLI\n  create <absolute-directory> <filename> [--template <template.json>]\n  validate <filename>\n  presets");
    return 0;
}
if (args is ["validate", var name])
{
    var error = FileNameValidator.Validate(name);
    Console.WriteLine(error ?? "Valid filename");
    return error is null ? 0 : 2;
}
if (args is ["presets"])
{
    foreach (var preset in BuiltIns.Presets()) Console.WriteLine($"{preset.Name,-16} {preset.DefaultFilename}");
    return 0;
}
if (args.Length is 3 or 5 && args[0] == "create" && (args.Length == 3 || args[3] == "--template"))
{
    try
    {
        var text = args.Length == 5 ? (await TemplateStore.ImportAsync(args[4])).Content : null;
        var result = await new FileCreationService().CreateAsync(args[1], args[2], text);
        if (result.Success) Console.WriteLine(result.Path);
        else Console.Error.WriteLine(result.Error);
        return result.Success ? 0 : 1;
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
    { Console.Error.WriteLine(ex.Message); return 1; }
}
Console.Error.WriteLine("Invalid arguments. Use --help.");
return 2;
