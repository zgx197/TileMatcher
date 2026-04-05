namespace TileMatcher.Logging;

/// <summary>
/// 调试面板读取日志时使用的快照数据。
/// </summary>
public sealed class LogSnapshot
{
    public bool IsInitialized { get; init; }

    public bool CanOpenDirectory { get; init; }

    public string LogDirectoryPath { get; init; } = string.Empty;

    public string LatestLogPath { get; init; } = string.Empty;

    public string LatestErrorLogPath { get; init; } = string.Empty;

    public string LatestLogText { get; init; } = string.Empty;

    public string LatestErrorLogText { get; init; } = string.Empty;
}
