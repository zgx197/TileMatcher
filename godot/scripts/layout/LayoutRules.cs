using TileMatcher.Data;
using TileMatcher.Grid;

namespace TileMatcher.Layout;

/// <summary>
/// 布局生成器与校验器共享的运行时规则对象。
/// </summary>
/// <remarks>
/// 配置资源会先转换成这份纯逻辑对象，再交给生成器和校验器使用。
/// 这样可以把编辑器数据层和运行时逻辑层解耦。
/// </remarks>
public sealed class LayoutRules
{
    /// <summary>允许的最少层数。</summary>
    public int MinLayerCount { get; init; } = 4;

    /// <summary>允许的最多层数。</summary>
    public int MaxLayerCount { get; init; } = 5;

    /// <summary>底层至少需要有多少张牌。</summary>
    public int MinBottomLayerTileCount { get; init; } = 12;

    /// <summary>是否要求上层牌底面被下一层完整覆盖。</summary>
    public bool RequireStrictSupport { get; init; } = true;

    /// <summary>是否要求每一层牌数严格少于下一层。</summary>
    public bool RequireUpperLayerStrictlySmaller { get; init; } = true;

    /// <summary>随机生成时底层宽度最小值，单位是牌数。</summary>
    public int RandomWidthMin { get; init; } = 4;

    /// <summary>随机生成时底层宽度最大值，单位是牌数。</summary>
    public int RandomWidthMax { get; init; } = 5;

    /// <summary>随机生成时底层高度最小值，单位是牌数。</summary>
    public int RandomHeightMin { get; init; } = 5;

    /// <summary>随机生成时底层高度最大值，单位是牌数。</summary>
    public int RandomHeightMax { get; init; } = 6;

    /// <summary>底层内部挖洞概率。</summary>
    public float BottomLayerHoleChance { get; init; } = 0.16f;

    /// <summary>随机生成最大尝试次数。</summary>
    public int GenerationMaxAttempts { get; init; } = 48;

    /// <summary>牌在逻辑层的宽度。</summary>
    public int TileWidthUnits { get; init; } = GridConfig.DefaultFootprintWidthUnits;

    /// <summary>牌在逻辑层的高度。</summary>
    public int TileHeightUnits { get; init; } = GridConfig.DefaultFootprintHeightUnits;

    /// <summary>底层牌阵在 X 方向的铺放步长。</summary>
    public int BottomLayerStepX { get; init; } = GridConfig.BottomLayerStepX;

    /// <summary>底层牌阵在 Y 方向的铺放步长。</summary>
    public int BottomLayerStepY { get; init; } = GridConfig.BottomLayerStepY;

    /// <summary>上层相对下层的偏移模式。</summary>
    public LayerOffsetMode UpperLayerOffsetMode { get; init; } = LayerOffsetMode.HalfXY;

    /// <summary>根据当前规则生成一份运行时 TileShape。</summary>
    public TileShape CreateTileShape()
    {
        return new TileShape
        {
            WidthUnits = TileWidthUnits,
            HeightUnits = TileHeightUnits,
        };
    }

    /// <summary>返回给 UI / 日志使用的人类可读偏移名称。</summary>
    public string GetOffsetModeDisplayName()
    {
        return UpperLayerOffsetMode switch
        {
            LayerOffsetMode.HalfX => "半宽",
            LayerOffsetMode.HalfY => "半高",
            LayerOffsetMode.HalfXY => "半宽+半高",
            _ => "对齐",
        };
    }
}
