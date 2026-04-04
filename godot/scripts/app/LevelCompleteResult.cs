namespace TileMatcher.App;

/// <summary>
/// 一局完成后传给结算页的最小结果数据。
/// 当前除了基础战绩，还会携带“是否存在每日奖励待展示”的轻量状态。
/// </summary>
public sealed class LevelCompleteResult
{
    public int LevelNumber { get; init; }

    public int Score { get; init; }

    public int MatchCount { get; init; }

    public string ElapsedText { get; init; } = "00:00";

    public int NextLevelNumber { get; init; }

    public string NextLevelName { get; init; } = string.Empty;

    public string NextLevelSummary { get; init; } = string.Empty;

    public bool HasDailyReward { get; init; }

    public int DailyRewardCoinCount { get; init; }
}
