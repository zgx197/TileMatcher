using System.Text.Json;
using System.Text.Json.Serialization;

namespace TileMatcher.Offline;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var repoRoot = ResolveRepoRoot();
            var configPath = ResolveConfigPath(args, repoRoot);
            var config = LoadConfig(configPath);

            Console.WriteLine($"[Offline] 批次名: {config.BatchName}");
            Console.WriteLine($"[Offline] 配置文件: {configPath}");

            var summary = OfflinePipeline.Run(config, repoRoot);
            Console.WriteLine($"[Offline] 候选总数: {summary.CandidateCount}");
            Console.WriteLine($"[Offline] 自动通过: {summary.AcceptedCount}");
            Console.WriteLine($"[Offline] 待复核: {summary.NeedsReviewCount}");
            Console.WriteLine($"[Offline] 自动淘汰: {summary.RejectedCount}");
            Console.WriteLine($"[Offline] 分析输出: {summary.AnalysisOutputDir}");
            Console.WriteLine($"[Offline] 分析台首页: {summary.AnalysisDashboardPath}");
            Console.WriteLine($"[Offline] 运行时关卡输出: {summary.RuntimeOutputDir}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[Offline] 执行失败: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static OfflineBatchConfig LoadConfig(string configPath)
    {
        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<OfflineBatchConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter() },
            });

        return config ?? throw new InvalidOperationException("配置文件解析失败。");
    }

    private static string ResolveConfigPath(string[] args, string repoRoot)
    {
        if (args.Length == 0)
        {
            return Path.Combine(repoRoot, "tools", "TileMatcher.Offline", "batch-config.sample.json");
        }

        return Path.GetFullPath(Path.Combine(repoRoot, args[0]));
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
