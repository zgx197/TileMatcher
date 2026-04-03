using Godot;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// 单个关卡的最小配置资源。
/// 它负责描述“进入这一关时，应该选什么规则档案、用什么布局来源、是否固定随机种子”。
/// </summary>
public partial class LevelConfig : Resource
{
    /// <summary>关卡编号，是外围流程和结算页共同使用的稳定编号。</summary>
    [Export]
    public int LevelNumber { get; set; } = 1;

    /// <summary>用于调试面板和日志展示的关卡名。</summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>进入这一关时要切换到的规则档案 id。</summary>
    [Export]
    public string LayoutProfileId { get; set; } = string.Empty;

    /// <summary>这一关采用固定原型还是正式生成布局。</summary>
    [Export]
    public LevelLayoutSourceMode LayoutSourceMode { get; set; } = LevelLayoutSourceMode.RandomGenerated;

    /// <summary>
    /// 是否固定随机种子。
    /// 为 true 时，同一关每次进入都会得到同一份布局，便于调试和验证。
    /// </summary>
    [Export]
    public bool UseFixedSeed { get; set; } = true;

    /// <summary>固定随机种子。仅在 <see cref="UseFixedSeed"/> 为 true 时生效。</summary>
    [Export]
    public int RandomSeed { get; set; } = 1001;

    /// <summary>
    /// 覆盖默认来源名，用于调试面板摘要显示。
    /// 为空时由运行时按模式自动生成，例如“关卡 4 随机布局”。
    /// </summary>
    [Export]
    public string SourceNameOverride { get; set; } = string.Empty;
}
