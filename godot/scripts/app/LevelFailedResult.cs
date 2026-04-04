namespace TileMatcher.App;

/// <summary>
/// 一局进入死局后传给失败页的最小结果数据。
/// </summary>
public sealed class LevelFailedResult
{
    public int LevelNumber { get; init; }

    public int Score { get; init; }

    public int MatchCount { get; init; }

    public string ElapsedText { get; init; } = "00:00";

    public int RetryLevelNumber { get; init; }

    public string FailureReason { get; init; } = "当前局面已经没有可继续配对的牌。";
}
