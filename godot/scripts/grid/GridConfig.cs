using Godot;

namespace TileMatcher.Grid;

/// <summary>
/// 网格与视觉尺寸之间的全局映射参数。
/// </summary>
/// <remarks>
/// 这里同时定义：
/// - 视觉尺寸：牌绘制多大
/// - 逻辑尺寸：牌在网格里占多少微单元
/// 二者共同决定一个逻辑微单元在世界空间中的实际大小。
/// </remarks>
public static class GridConfig
{
    /// <summary>单张麻将的目标视觉宽度。</summary>
    public const float TileWidth = 96.0f;
    /// <summary>单张麻将的目标视觉高度。</summary>
    public const float TileHeight = 128.0f;
    /// <summary>当前用于绘制厚度感的参考值。</summary>
    public const float TileThickness = 10.0f;
    /// <summary>上层相对下层的视觉 X 偏移。</summary>
    public const float LayerOffsetX = -10.0f;
    /// <summary>上层相对下层的视觉 Y 偏移。</summary>
    public const float LayerOffsetY = -16.0f;
    /// <summary>默认标准麻将在逻辑层的宽度。</summary>
    public const int DefaultFootprintWidthUnits = 4;
    /// <summary>默认标准麻将在逻辑层的高度。</summary>
    public const int DefaultFootprintHeightUnits = 6;
    /// <summary>底层对齐铺牌时 X 方向的默认步长。</summary>
    public const int BottomLayerStepX = DefaultFootprintWidthUnits;
    /// <summary>底层对齐铺牌时 Y 方向的默认步长。</summary>
    public const int BottomLayerStepY = DefaultFootprintHeightUnits;

    /// <summary>一个逻辑微单元在世界空间中的宽度。</summary>
    public static float CellWidth => TileWidth / DefaultFootprintWidthUnits;
    /// <summary>一个逻辑微单元在世界空间中的高度。</summary>
    public static float CellHeight => TileHeight / DefaultFootprintHeightUnits;

    /// <summary>标准牌的视觉尺寸。</summary>
    public static Vector2 TileSize => new(TileWidth, TileHeight);
    /// <summary>单层视觉偏移向量。</summary>
    public static Vector2 LayerVisualOffset => new(LayerOffsetX, LayerOffsetY);
}
