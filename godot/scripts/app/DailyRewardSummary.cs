namespace TileMatcher.App;

/// <summary>
/// 每日奖励页展示数据。
/// 用于把奖励展示和流程跳转参数集中打包，避免页面层再回头查询流程状态。
/// </summary>
public sealed class DailyRewardSummary
{
    public string RewardTitle { get; init; } = "每日奖励";

    public string RewardDescription { get; init; } = string.Empty;

    public int RewardCoinCount { get; init; }

    public int CurrentCoinTotal { get; init; }

    public int NextLevelNumber { get; init; }

    public string NextLevelName { get; init; } = string.Empty;

    public string NextLevelSummary { get; init; } = string.Empty;
}
