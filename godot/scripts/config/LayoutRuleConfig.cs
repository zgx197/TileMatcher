using Godot;
using TileMatcher.Data;
using TileMatcher.Grid;
using TileMatcher.Layout;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// Godot 侧可编辑的布局规则资源。
/// </summary>
/// <remarks>
/// 它负责承载 Inspector 里的配置项，
/// 运行时会先转换成 LayoutRules，再交给纯逻辑层使用。
/// </remarks>
public partial class LayoutRuleConfig : Resource
{
    /// <summary>最少层数。</summary>
    [Export]
    public int MinLayerCount { get; set; } = 4;

    /// <summary>最多层数。</summary>
    [Export]
    public int MaxLayerCount { get; set; } = 5;

    /// <summary>底层最少牌数。</summary>
    [Export]
    public int MinBottomLayerTileCount { get; set; } = 12;

    /// <summary>是否要求完整底面覆盖。</summary>
    [Export]
    public bool RequireStrictSupport { get; set; } = true;

    /// <summary>是否要求上层牌数严格少于下层。</summary>
    [Export]
    public bool RequireUpperLayerStrictlySmaller { get; set; } = true;

    /// <summary>随机生成底层最小宽度。</summary>
    [Export]
    public int RandomWidthMin { get; set; } = 4;

    /// <summary>随机生成底层最大宽度。</summary>
    [Export]
    public int RandomWidthMax { get; set; } = 5;

    /// <summary>随机生成底层最小高度。</summary>
    [Export]
    public int RandomHeightMin { get; set; } = 5;

    /// <summary>随机生成底层最大高度。</summary>
    [Export]
    public int RandomHeightMax { get; set; } = 6;

    /// <summary>底层内部随机挖洞概率。</summary>
    [Export]
    public float BottomLayerHoleChance { get; set; } = 0.16f;

    /// <summary>生成失败时允许的最大重试次数。</summary>
    [Export]
    public int GenerationMaxAttempts { get; set; } = 48;

    /// <summary>底层对齐步长 X。</summary>
    [Export]
    public int BottomLayerStepX { get; set; } = GridConfig.BottomLayerStepX;

    /// <summary>底层对齐步长 Y。</summary>
    [Export]
    public int BottomLayerStepY { get; set; } = GridConfig.BottomLayerStepY;

    /// <summary>上层相对下层的偏移模式。</summary>
    [Export]
    public LayerOffsetMode UpperLayerOffsetMode { get; set; } = LayerOffsetMode.HalfXY;

    /// <summary>当前规则引用的牌形资源。</summary>
    [Export]
    public TileShapeConfig TileShape { get; set; } = null!;

    /// <summary>转换成纯运行时规则对象。</summary>
    public LayoutRules ToRuntimeRules()
    {
        var runtimeShape = TileShape?.ToRuntimeShape() ?? TileMatcher.Data.TileShape.StandardTile;

        return new LayoutRules
        {
            MinLayerCount = MinLayerCount,
            MaxLayerCount = MaxLayerCount,
            MinBottomLayerTileCount = MinBottomLayerTileCount,
            RequireStrictSupport = RequireStrictSupport,
            RequireUpperLayerStrictlySmaller = RequireUpperLayerStrictlySmaller,
            RandomWidthMin = RandomWidthMin,
            RandomWidthMax = RandomWidthMax,
            RandomHeightMin = RandomHeightMin,
            RandomHeightMax = RandomHeightMax,
            BottomLayerHoleChance = BottomLayerHoleChance,
            GenerationMaxAttempts = GenerationMaxAttempts,
            TileWidthUnits = runtimeShape.WidthUnits,
            TileHeightUnits = runtimeShape.HeightUnits,
            BottomLayerStepX = BottomLayerStepX,
            BottomLayerStepY = BottomLayerStepY,
            UpperLayerOffsetMode = UpperLayerOffsetMode,
        };
    }
}
