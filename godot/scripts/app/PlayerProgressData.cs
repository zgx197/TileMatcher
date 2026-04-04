using System.Collections.Generic;

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

    /// <summary>
    /// 每关辅助资源使用情况。
    /// key 直接使用关卡号，便于运行时读取和 JSON 调试。
    /// </summary>
    public Dictionary<int, LevelAssistUsageData> LevelAssistUsageByLevel { get; set; } = [];

    /// <summary>
    /// 取得某一关的辅助资源使用状态，不存在时自动补默认值。
    /// </summary>
    public LevelAssistUsageData GetOrCreateLevelAssistUsage(int levelNumber)
    {
        var normalizedLevelNumber = levelNumber < 1 ? 1 : levelNumber;
        if (!LevelAssistUsageByLevel.TryGetValue(normalizedLevelNumber, out var usage) || usage is null)
        {
            usage = new LevelAssistUsageData();
            LevelAssistUsageByLevel[normalizedLevelNumber] = usage;
        }

        return usage;
    }
}
