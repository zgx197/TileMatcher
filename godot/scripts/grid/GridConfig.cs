using Godot;

namespace TileMatcher.Grid;

public static class GridConfig
{
    public const float TileWidth = 96.0f;
    public const float TileHeight = 128.0f;
    public const float TileThickness = 10.0f;
    public const float LayerOffsetX = -10.0f;
    public const float LayerOffsetY = -16.0f;

    public static float StepX => TileWidth * 0.5f;
    public static float StepY => TileHeight * 0.5f;

    public static Vector2 TileSize => new(TileWidth, TileHeight);
    public static Vector2 LayerVisualOffset => new(LayerOffsetX, LayerOffsetY);
}
