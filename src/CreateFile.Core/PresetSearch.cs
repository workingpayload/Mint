namespace CreateFile.Core;
public static class PresetSearch
{
    // Matches name, extension or default filename, ignoring case, spaces, dots, dashes and underscores,
    // so "gitignore", "env", ".py" and "react" style queries behave as users expect.
    // Ranking: exact field match, then prefix, then substring; favorites first within a rank; otherwise stable.
    public static IEnumerable<FilePreset> Find(IEnumerable<FilePreset> presets, string? query)
    {
        var needle = Normalize(query);
        return presets
            .Select((preset, index) => (preset, index, rank: needle.Length == 0 ? 0 : Rank(preset, needle)))
            .Where(match => match.rank >= 0)
            .OrderBy(match => match.rank).ThenByDescending(match => match.preset.Pinned).ThenBy(match => match.index)
            .Select(match => match.preset);
    }
    private static int Rank(FilePreset preset, string needle)
    {
        var best = -1;
        foreach (var field in new[] { preset.Name, preset.Extension, preset.DefaultFilename })
        {
            var value = Normalize(field);
            if (value.Length == 0) continue;
            var rank = value == needle ? 0 : value.StartsWith(needle, StringComparison.Ordinal) ? 1 : value.Contains(needle, StringComparison.Ordinal) ? 2 : -1;
            if (rank >= 0 && (best < 0 || rank < best)) best = rank;
        }
        return best;
    }
    private static string Normalize(string? value) =>
        string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-' or '_' or '★'))).ToLowerInvariant();
}
