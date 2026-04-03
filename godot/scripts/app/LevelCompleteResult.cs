namespace TileMatcher.App;

/// <summary>
/// 一局完成后传递给结算页的最小结果数据。
/// </summary>
/// <remarks>
/// 当前阶段只保留“串流程”所需字段：
/// - 关卡号
/// - 分数
/// - 成功配对数
/// - 用时文本
///
/// 后续如果要补充连击、评价、奖励进度或每日奖励触发状态，
/// 应继续在这个对象上扩展，而不是把数据散落到多个页面节点属性中。
/// </remarks>
public sealed class LevelCompleteResult
{
    public int LevelNumber { get; init; }

    public int Score { get; init; }

    public int MatchCount { get; init; }

    public string ElapsedText { get; init; } = "00:00";

    /// <summary>结算页中显示的下一关编号。</summary>
    public int NextLevelNumber { get; init; }

    /// <summary>下一关名称，用于做“继续”前的正式预告文案。</summary>
    public string NextLevelName { get; init; } = string.Empty;

    /// <summary>下一关规则摘要，用于结算页底部预告说明。</summary>
    public string NextLevelSummary { get; init; } = string.Empty;
}
