namespace TileMatcher.App;

/// <summary>
/// 每日奖励页展示数据。
/// 用于把奖励说明、金币变化和下一关信息集中打包，
/// 避免奖励页再反查外围流程状态。
/// </summary>
public sealed class DailyRewardSummary
{
    /// <summary>奖励页标题，例如“今日金币奖励”。</summary>
    public string RewardTitle { get; init; } = "每日奖励";

    /// <summary>奖励说明文案。</summary>
    public string RewardDescription { get; init; } = string.Empty;

    /// <summary>本次奖励发放的金币数量。</summary>
    public int RewardCoinCount { get; init; }

    /// <summary>奖励到账后的当前金币总量。</summary>
    public int CurrentCoinTotal { get; init; }

    /// <summary>点击继续后要进入的关卡号。</summary>
    public int NextLevelNumber { get; init; }

    /// <summary>下一关的展示名称。</summary>
    public string NextLevelName { get; init; } = string.Empty;

    /// <summary>下一关的简短说明。</summary>
    public string NextLevelSummary { get; init; } = string.Empty;
}
