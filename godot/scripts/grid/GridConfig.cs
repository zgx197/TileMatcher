using Godot;

namespace TileMatcher.Grid;

public static class GridConfig
{
    public const float TileWidth = 96.0f;
    public const float TileHeight = 128.0f;
    public const float TileThickness = 10.0f;
    public const float LayerOffsetX = -10.0f;
    public const float LayerOffsetY = -16.0f;
    public const int DefaultFootprintWidthUnits = 4;
    public const int DefaultFootprintHeightUnits = 6;
    public const int BottomLayerStepX = DefaultFootprintWidthUnits;
    public const int BottomLayerStepY = DefaultFootprintHeightUnits;

    public static float CellWidth => TileWidth / DefaultFootprintWidthUnits;
    public static float CellHeight => TileHeight / DefaultFootprintHeightUnits;

    public static Vector2 TileSize => new(TileWidth, TileHeight);
    public static Vector2 LayerVisualOffset => new(LayerOffsetX, LayerOffsetY);
}
