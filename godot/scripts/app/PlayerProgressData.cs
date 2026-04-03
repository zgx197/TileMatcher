namespace TileMatcher.App;

/// <summary>
/// 玩家外围进度数据。
/// 这里只保存主页、奖励页和关卡流转真正需要的最小状态，不把牌桌内部状态混进来。
/// </summary>
public sealed class PlayerProgressData
{
    public string PlayerName { get; set; } = "青瓷旅人";

    public int LeafCount { get; set; } = 1;

    public int CurrentLevelNumber { get; set; } = 1;

    public int HighestUnlockedLevel { get; set; } = 1;

    public int TotalScore { get; set; }

    public int TotalMatches { get; set; }

    public int TotalCompletedLevelCount { get; set; }

    public string LastDailyRewardDate { get; set; } = string.Empty;
}
