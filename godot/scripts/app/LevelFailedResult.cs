namespace TileMatcher.App;

/// <summary>
/// 一局进入死局后传给失败页的最小结果数据。
/// </summary>
public sealed class LevelFailedResult
{
    /// <summary>失败时所在的关卡号。</summary>
    public int LevelNumber { get; init; }

    /// <summary>失败前累计得到的分数。</summary>
    public int Score { get; init; }

    /// <summary>失败前累计完成的配对次数。</summary>
    public int MatchCount { get; init; }

    /// <summary>失败时展示的本局耗时文本。</summary>
    public string ElapsedText { get; init; } = "00:00";

    /// <summary>重试按钮应重新进入的关卡号。</summary>
    public int RetryLevelNumber { get; init; }

    /// <summary>展示给玩家的失败原因说明。</summary>
    public string FailureReason { get; init; } = "当前局面已经没有可继续配对的牌。";
}
