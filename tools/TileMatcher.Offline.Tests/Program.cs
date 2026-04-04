using System.Text.Json;

namespace TileMatcher.Offline.Tests;

internal static class Program
{
    public static int Main()
    {
        var repoRoot = ResolveRepoRoot();
        var results = OfflinePipelineTests.RunAll(repoRoot);
        var summary = BuildSummary(results);
        WriteSummary(repoRoot, summary);
        PrintSummary(summary);
        return summary.FailedCount == 0 ? 0 : 1;
    }

    private static TestRunSummary BuildSummary(IReadOnlyList<TestCaseResult> results)
    {
        return new TestRunSummary
        {
            TotalCount = results.Count,
            PassedCount = results.Count(result => result.Passed),
            FailedCount = results.Count(result => !result.Passed),
            Results = results.ToList(),
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }

    private static void WriteSummary(string repoRoot, TestRunSummary summary)
    {
        var outputDir = Path.Combine(repoRoot, "artifacts", "test-results", "offline-tests");
        Directory.CreateDirectory(outputDir);
        var summaryPath = Path.Combine(outputDir, "summary.json");
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(summaryPath, json);
    }

    private static void PrintSummary(TestRunSummary summary)
    {
        Console.WriteLine($"[OfflineTests] Total={summary.TotalCount} Passed={summary.PassedCount} Failed={summary.FailedCount}");
        foreach (var result in summary.Results)
        {
            if (result.Passed)
            {
                Console.WriteLine($"[PASS] {result.Name}");
            }
            else
            {
                Console.WriteLine($"[FAIL] {result.Name}: {result.Message}");
            }
        }
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "godot")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("无法定位仓库根目录。");
    }
}

internal sealed class TestRunSummary
{
    public int TotalCount { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public List<TestCaseResult> Results { get; init; } = [];
}

internal sealed class TestCaseResult
{
    public string Name { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;

    public static TestCaseResult CreatePassed(string name)
    {
        return new TestCaseResult
        {
            Name = name,
            Passed = true,
        };
    }

    public static TestCaseResult CreateFailed(string name, string message)
    {
        return new TestCaseResult
        {
            Name = name,
            Passed = false,
            Message = message,
        };
    }
}
