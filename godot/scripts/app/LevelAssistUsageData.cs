namespace TileMatcher.App;

/// <summary>
/// 单关辅助资源的使用情况。
/// 当前只记录“重新开始”和“提示”已经消耗了多少次。
/// </summary>
public sealed class LevelAssistUsageData
{
    public int RestartUsedCount { get; set; }

    public int HintUsedCount { get; set; }
}
