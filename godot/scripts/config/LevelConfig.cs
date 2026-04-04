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

    [Export(PropertyHint.File, "*.json")]
    public string OfflineLayoutJsonPath { get; set; } = string.Empty;

    /// <summary>
    /// 离线侧导出的关卡目录索引文件，通常是 level-catalog.json。
    /// 配置后，运行时会优先按目录索引定位当前关卡对应的 JSON 文件。
    /// </summary>
    [Export(PropertyHint.File, "*.json")]
    public string OfflineCatalogJsonPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否把这份关卡配置当作“离线目录模板”使用。
    /// 启用后，未显式配置的关卡号可以复用这份配置，并在运行时覆盖 LevelNumber。
    /// </summary>
    [Export]
    public bool UseAsOfflineCatalogTemplate { get; set; }

    public LevelConfig CreateResolvedCopy(int resolvedLevelNumber)
    {
        return new LevelConfig
        {
            LevelNumber = resolvedLevelNumber,
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) || UseAsOfflineCatalogTemplate
                ? $"第 {resolvedLevelNumber} 关"
                : DisplayName,
            LayoutProfileId = LayoutProfileId,
            LayoutSourceMode = LayoutSourceMode,
            UseFixedSeed = UseFixedSeed,
            RandomSeed = RandomSeed,
            SourceNameOverride = string.IsNullOrWhiteSpace(SourceNameOverride) || UseAsOfflineCatalogTemplate
                ? $"关卡 {resolvedLevelNumber} 离线正式牌局"
                : SourceNameOverride,
            OfflineLayoutJsonPath = OfflineLayoutJsonPath,
            OfflineCatalogJsonPath = OfflineCatalogJsonPath,
            UseAsOfflineCatalogTemplate = UseAsOfflineCatalogTemplate,
        };
    }
}
