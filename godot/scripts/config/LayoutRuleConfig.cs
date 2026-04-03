using Godot;
using TileMatcher.Data;
using TileMatcher.Grid;
using TileMatcher.Layout;

namespace TileMatcher.Config;

[GlobalClass]
public partial class LayoutRuleConfig : Resource
{
    // 规则资源只负责承载可编辑配置，运行时再转换成纯逻辑对象。
    [Export]
    public int MinLayerCount { get; set; } = 4;

    [Export]
    public int MaxLayerCount { get; set; } = 5;

    [Export]
    public int MinBottomLayerTileCount { get; set; } = 12;

    [Export]
    public bool RequireStrictSupport { get; set; } = true;

    [Export]
    public bool RequireUpperLayerStrictlySmaller { get; set; } = true;

    [Export]
    public int RandomWidthMin { get; set; } = 4;

    [Export]
    public int RandomWidthMax { get; set; } = 5;

    [Export]
    public int RandomHeightMin { get; set; } = 5;

    [Export]
    public int RandomHeightMax { get; set; } = 6;

    [Export]
    public float BottomLayerHoleChance { get; set; } = 0.16f;

    [Export]
    public int GenerationMaxAttempts { get; set; } = 48;

    [Export]
    public int BottomLayerStepX { get; set; } = GridConfig.BottomLayerStepX;

    [Export]
    public int BottomLayerStepY { get; set; } = GridConfig.BottomLayerStepY;

    [Export]
    public LayerOffsetMode UpperLayerOffsetMode { get; set; } = LayerOffsetMode.HalfXY;

    [Export]
    public TileShapeConfig TileShape { get; set; } = null!;

    public LayoutRules ToRuntimeRules()
    {
        var runtimeShape = TileShape?.ToRuntimeShape() ?? TileMatcher.Data.TileShape.StandardMahjong;

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
