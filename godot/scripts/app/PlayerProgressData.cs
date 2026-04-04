using System.Collections.Generic;

namespace TileMatcher.App;

/// <summary>
/// 玩家外围进度数据。
/// 这里只保存首页、奖励页和关卡流转真正需要的最小状态，
/// 不把单局牌桌内部状态直接混入长期存档。
/// </summary>
public sealed class PlayerProgressData
{
    /// <summary>首页顶部默认显示名，当前用于展示游戏名称。</summary>
    public string PlayerName { get; set; } = "毛球碰碰乐";

    /// <summary>外围可消耗资源“金币”的当前数量。</summary>
    public int CoinCount { get; set; } = 1;

    /// <summary>当前流程准备进入或继续的关卡号。</summary>
    public int CurrentLevelNumber { get; set; } = 1;

    /// <summary>历史上已经解锁到的最高关卡号。</summary>
    public int HighestUnlockedLevel { get; set; } = 1;

    /// <summary>累计得分。</summary>
    public int TotalScore { get; set; }

    /// <summary>累计完成的配对数。</summary>
    public int TotalMatches { get; set; }

    /// <summary>累计完成过的关卡局数。</summary>
    public int TotalCompletedLevelCount { get; set; }

    /// <summary>上一回领取每日奖励时对应的日期字符串。</summary>
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
