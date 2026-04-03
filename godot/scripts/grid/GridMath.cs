using System.Collections.Generic;
using System.Linq;
using Godot;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Grid;

public static class GridMath
{
    public static Rect2I GetFootprint(AppTileData tile)
    {
        return new Rect2I(tile.GX, tile.GY, 2, 2);
    }

    public static bool OverlapsXY(AppTileData a, AppTileData b)
    {
        return a.GX < b.GX + 2
            && a.GX + 2 > b.GX
            && a.GY < b.GY + 2
            && a.GY + 2 > b.GY;
    }

    public static Vector2 GridToWorld(int gx, int gy, int gz, Vector2 boardOrigin)
    {
        return boardOrigin
            + new Vector2(gx * GridConfig.StepX, gy * GridConfig.StepY)
            + GridConfig.LayerVisualOffset * gz;
    }

    public static bool HasAnyAboveOverlap(AppTileData tile, IEnumerable<AppTileData> allTiles)
    {
        foreach (var other in allTiles)
        {
            if (other.Removed || other.Id == tile.Id || other.GZ <= tile.GZ)
            {
                continue;
            }

            if (OverlapsXY(tile, other))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasFullSupportFromLowerLayer(AppTileData tile, IReadOnlyCollection<AppTileData> lowerLayerTiles)
    {
        if (tile.GZ <= 0)
        {
            return true;
        }

        var lowerLayerPositions = lowerLayerTiles
            .Where(lowerTile => !lowerTile.Removed)
            .Select(lowerTile => new Vector2I(lowerTile.GX, lowerTile.GY))
            .ToHashSet();

        return lowerLayerPositions.Contains(new Vector2I(tile.GX - 1, tile.GY - 1))
            && lowerLayerPositions.Contains(new Vector2I(tile.GX + 1, tile.GY - 1))
            && lowerLayerPositions.Contains(new Vector2I(tile.GX - 1, tile.GY + 1))
            && lowerLayerPositions.Contains(new Vector2I(tile.GX + 1, tile.GY + 1));
    }

    public static Rect2 GetWorldBounds(IEnumerable<AppTileData> tiles)
    {
        var hasAny = false;
        var minX = 0.0f;
        var minY = 0.0f;
        var maxX = 0.0f;
        var maxY = 0.0f;

        foreach (var tile in tiles)
        {
            if (tile.Removed)
            {
                continue;
            }

            var worldPos = GridToWorld(tile.GX, tile.GY, tile.GZ, Vector2.Zero);
            var rect = new Rect2(worldPos, GridConfig.TileSize);

            if (!hasAny)
            {
                hasAny = true;
                minX = rect.Position.X;
                minY = rect.Position.Y;
                maxX = rect.End.X;
                maxY = rect.End.Y;
                continue;
            }

            minX = Mathf.Min(minX, rect.Position.X);
            minY = Mathf.Min(minY, rect.Position.Y);
            maxX = Mathf.Max(maxX, rect.End.X);
            maxY = Mathf.Max(maxY, rect.End.Y);
        }

        if (!hasAny)
        {
            return new Rect2();
        }

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }
}
