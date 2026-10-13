using System.Reflection;
using System.Collections;
namespace CreateFile.Testing;
public sealed class SkipException(string message) : Exception(message);

// Small offline runner. Each case has a fresh fixture; failures produce a nonzero exit code.
[AttributeUsage(AttributeTargets.Method)] public sealed class FactAttribute : Attribute;
[AttributeUsage(AttributeTargets.Method)] public sealed class TheoryAttribute : Attribute;
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InlineDataAttribute(params object[] values) : Attribute { public object[] Values => values; }
public static class Assert
{
    public static void True(bool value, string? message = null) { if (!value) throw new Exception(message ?? "Expected true."); }
    public static void False(bool value) => True(!value, "Expected false.");
    public static void NotNull(object? value) => True(value is not null, "Expected non-null.");
    public static void Equal<T>(T expected, T actual)
    {
        if (expected is IEnumerable a && actual is IEnumerable b && expected is not string)
            True(a.Cast<object>().SequenceEqual(b.Cast<object>()), "Sequences differ.");
        else True(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}, got {actual}.");
    }
    public static void NotEqual<T>(T a, T b) => True(!EqualityComparer<T>.Default.Equals(a, b));
    public static void Empty(IEnumerable values) => True(!values.Cast<object>().Any());
    public static void NotEmpty(IEnumerable values) => True(values.Cast<object>().Any());
    public static void Single(IEnumerable values) => Equal(1, values.Cast<object>().Count());
    public static void Contains(string value, string? text) => True(text?.Contains(value) == true);
    public static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? filter = null;
        bool requireNoSkips = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--require-no-skips") requireNoSkips = true;
            else if (args[i] == "--filter" && i + 1 < args.Length) filter = args[++i];
            else { Console.Error.WriteLine("Usage: [--filter <test-name>] [--require-no-skips]"); return 2; }
        }
        int passed = 0, failed = 0, skipped = 0;
        foreach (var method in typeof(CreateFile.Core.Tests.CoreTests).GetMethods())
        {
            if (method.GetCustomAttribute<FactAttribute>() is null && method.GetCustomAttribute<TheoryAttribute>() is null) continue;
            if (filter is not null && !method.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            var cases = method.GetCustomAttributes<InlineDataAttribute>().Select(a => a.Values).ToArray();
            if (cases.Length == 0) cases = [Array.Empty<object>()];
            foreach (var data in cases)
            {
                using var fixture = new CreateFile.Core.Tests.CoreTests();
                try
                {
                    if (method.Invoke(fixture, data) is Task task) await task;
                    Console.WriteLine($"PASS {method.Name}({string.Join(", ", data)})");
                    passed++;
                }
                catch (Exception e) when (e is SkipException || e.InnerException is SkipException) { Console.WriteLine($"SKIP {method.Name}: {(e.InnerException ?? e).Message}"); skipped++; }
                catch (Exception e) { Console.Error.WriteLine($"FAIL {method.Name}: {e.InnerException ?? e}"); failed++; }
            }
        }
        Console.WriteLine($"{passed} passed, {failed} failed, {skipped} skipped.");
        if (passed + failed + skipped == 0) { Console.Error.WriteLine("No matching tests."); return 2; }
        return failed == 0 && (!requireNoSkips || skipped == 0) ? 0 : 1;
    }
}
