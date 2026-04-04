namespace TileMatcher.Data;

/// <summary>
/// 背面牌在运行时的可见状态机。
/// </summary>
public enum TileFaceState
{
    /// <summary>普通正面牌，不参与背面牌状态机。</summary>
    None = 0,

    /// <summary>背面待机态，尚未被玩家激活。</summary>
    FaceDownIdle = 1,

    /// <summary>当前已被激活并翻到正面的背面牌。</summary>
    ActiveFaceUp = 2,

    /// <summary>该牌已经参与成功配对，后续会被移出牌桌。</summary>
    ClearedMatched = 3,

    /// <summary>背面牌结束当前激活后翻回背面。</summary>
    CollapsedBack = 4,
}

/// <summary>
/// 单张牌在运行时的纯数据表示。
/// </summary>
/// <remarks>
/// 这里故意不放任何 Godot 节点引用，只承载布局、几何和玩法相关的数据。
/// 当前系统采用“左上角锚点 + 矩形 footprint”的表达方式：
/// - GX / GY 是牌在逻辑网格中的左上角坐标
/// - GZ 是层级
/// - Shape 决定这张牌在二维平面上覆盖多少微单元
/// 
/// 这份数据会同时被布局生成器、校验器和视图层使用，
/// 因此字段含义一旦变化，会影响整个堆叠框架。
/// </remarks>
public sealed class TileData
{
    /// <summary>布局内唯一标识，主要用于日志和调试。</summary>
    public int Id { get; init; }

    /// <summary>牌面类型编码，后续会参与配对逻辑。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>逻辑网格 X 坐标，表示 footprint 左上角。</summary>
    public int GX { get; init; }

    /// <summary>逻辑网格 Y 坐标，表示 footprint 左上角。</summary>
    public int GY { get; init; }

    /// <summary>层级坐标，0 为底层。</summary>
    public int GZ { get; init; }

    /// <summary>当前牌的逻辑形状，默认使用标准 4x6 牌。</summary>
    public TileShape Shape { get; init; } = TileShape.StandardTile;

    /// <summary>便捷访问当前牌 footprint 的宽度，单位为逻辑网格微单元。</summary>
    public int FootprintWidth => Shape.WidthUnits;

    /// <summary>便捷访问当前牌 footprint 的高度，单位为逻辑网格微单元。</summary>
    public int FootprintHeight => Shape.HeightUnits;

    /// <summary>运行时是否已被移除，不参与后续交互与统计。</summary>
    public bool Removed { get; set; }

    /// <summary>运行时是否可移动，供交互层和调试摘要直接读取。</summary>
    public bool Movable { get; set; }

    public bool FaceHiddenInitial { get; init; }

    /// <summary>背面牌当前所处的运行时状态。</summary>
    public TileFaceState FaceState { get; set; } = TileFaceState.None;

    /// <summary>当前牌面是否以正面渲染。</summary>
    public bool IsFaceUp { get; set; } = true;
}
