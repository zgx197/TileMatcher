using System.Collections.Generic;
using TileMatcher.Pets;

namespace TileMatcher.App;

/// <summary>
/// 玩家外围进度数据。
/// 这里只保存首页、奖励页、救助中心和关卡流转真正需要的最小状态，
/// 不把单局棋盘内部状态直接混入长期存档。
/// </summary>
public sealed class PlayerProgressData
{
    /// <summary>首页顶部默认显示名，当前用于展示游戏名称。</summary>
    public string PlayerName { get; set; } = "毛球碰碰乐";

    /// <summary>外围可消耗资源“金币”的当前数量。</summary>
    public int CoinCount { get; set; } = 10;

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

    /// <summary>玩家已经领养的宠物列表。</summary>
    public List<OwnedPetData> OwnedPets { get; set; } = [];

    /// <summary>救助中心上一次刷新的北京时间文本。</summary>
    public string LastRescueRefreshBeijingTime { get; set; } = string.Empty;

    /// <summary>当前救助中心待领养宠物 id 列表。</summary>
    public List<string> RescueCenterPetIds { get; set; } = [];

    /// <summary>
    /// 每关辅助资源使用情况。
    /// key 直接使用关卡号，便于运行时读取和 JSON 调试。
    /// </summary>
    public Dictionary<int, LevelAssistUsageData> LevelAssistUsageByLevel { get; set; } = [];

    /// <summary>判断玩家是否已经领养过某只宠物。</summary>
    public bool HasAdoptedPet(string petId)
    {
        if (string.IsNullOrWhiteSpace(petId))
        {
            return false;
        }

        foreach (var ownedPet in OwnedPets)
        {
            if (ownedPet is not null && ownedPet.PetId == petId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>从当前救助中心列表中移除某只宠物。</summary>
    public void RemoveRescuePet(string petId)
    {
        RescueCenterPetIds.RemoveAll(id => id == petId);
    }

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
