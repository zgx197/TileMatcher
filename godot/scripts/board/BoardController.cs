using Godot;
using TileMatcher.Data;
using TileMatcher.Grid;
using TileMatcher.Tile;

namespace TileMatcher.Board;

public partial class BoardController : Node2D
{
    private const float SidePadding = 36.0f;
    private const float TopReservedHeight = 132.0f;
    private const float BottomReservedHeight = 172.0f;

    [Export]
    public PackedScene TileScene { get; set; } = null!;

    public override void _Ready()
    {
        if (TileScene is null)
        {
            TileScene = GD.Load<PackedScene>("res://scenes/tile/Tile.tscn");
        }

        var layout = LevelLayout.CreatePrototype();
        var boardOrigin = ComputeCenteredOrigin(layout);

        foreach (var tileData in layout.Tiles)
        {
            var tile = TileScene.Instantiate<TileView>();
            tile.ApplyData(tileData);
            tile.Position = GridMath.GridToWorld(tileData.GX, tileData.GY, boardOrigin);
            tile.ZIndex = tileData.GZ * 10000 + tileData.GY * 100 + tileData.GX;

            AddChild(tile);
        }
    }

    private Vector2 ComputeCenteredOrigin(LevelLayout layout)
    {
        var viewportSize = GetViewportRect().Size;
        var bounds = GridMath.GetWorldBounds(layout.Tiles);
        var boardAreaPosition = new Vector2(SidePadding, TopReservedHeight);
        var boardAreaSize = new Vector2(
            Mathf.Max(0.0f, viewportSize.X - SidePadding * 2.0f),
            Mathf.Max(0.0f, viewportSize.Y - TopReservedHeight - BottomReservedHeight));

        return boardAreaPosition + (boardAreaSize - bounds.Size) * 0.5f - bounds.Position;
    }
}
