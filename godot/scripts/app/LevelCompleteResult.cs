namespace TileMatcher.App;

/// <summary>
/// 一局完成后传给通关页的最小结果数据。
/// 除了成绩摘要，还会携带“是否还有每日奖励页待展示”的流程信息。
/// </summary>
public sealed class LevelCompleteResult
{
    /// <summary>已完成的关卡号。</summary>
    public int LevelNumber { get; init; }

    /// <summary>本局得分。</summary>
    public int Score { get; init; }

    /// <summary>本局成功配对次数。</summary>
    public int MatchCount { get; init; }

    /// <summary>本局耗时文本。</summary>
    public string ElapsedText { get; init; } = "00:00";

    /// <summary>继续按钮对应的下一关关卡号。</summary>
    public int NextLevelNumber { get; init; }

    /// <summary>下一关的展示名称。</summary>
    public string NextLevelName { get; init; } = string.Empty;

    /// <summary>下一关的简短摘要。</summary>
    public string NextLevelSummary { get; init; } = string.Empty;

    /// <summary>本次通关后是否还要进入每日奖励页。</summary>
    public bool HasDailyReward { get; init; }

    /// <summary>若存在每日奖励，本次奖励的金币数量。</summary>
    public int DailyRewardCoinCount { get; init; }
}
