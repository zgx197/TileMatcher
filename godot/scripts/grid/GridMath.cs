using System.Collections.Generic;
using System.Linq;
using Godot;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Grid;

public static class GridMath
{
    public static Rect2I GetFootprint(AppTileData tile)
    {
        return new Rect2I(tile.GX, tile.GY, tile.FootprintWidth, tile.FootprintHeight);
    }

    public static bool OverlapsXY(AppTileData a, AppTileData b)
    {
        var rectA = GetFootprint(a);
        var rectB = GetFootprint(b);

        return rectA.Position.X < rectB.End.X
            && rectA.End.X > rectB.Position.X
            && rectA.Position.Y < rectB.End.Y
            && rectA.End.Y > rectB.Position.Y;
    }

    public static Vector2 GridToWorld(int gx, int gy, int gz, Vector2 boardOrigin)
    {
        return boardOrigin
            + new Vector2(gx * GridConfig.CellWidth, gy * GridConfig.CellHeight)
            + GridConfig.LayerVisualOffset * gz;
    }

    public static Vector2 GetWorldSize(AppTileData tile)
    {
        return new Vector2(
            tile.FootprintWidth * GridConfig.CellWidth,
            tile.FootprintHeight * GridConfig.CellHeight);
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

        var footprint = GetFootprint(tile);
        var activeLowerTiles = lowerLayerTiles
            .Where(lowerTile => !lowerTile.Removed)
            .ToList();

        for (var y = footprint.Position.Y; y < footprint.End.Y; y++)
        {
            for (var x = footprint.Position.X; x < footprint.End.X; x++)
            {
                if (!IsCoveredByAnyTile(x, y, activeLowerTiles))
                {
                    return false;
                }
            }
        }

        return true;
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
            var rect = new Rect2(worldPos, GetWorldSize(tile));

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

    private static bool IsCoveredByAnyTile(int x, int y, IReadOnlyCollection<AppTileData> tiles)
    {
        foreach (var tile in tiles)
        {
            if (ContainsCell(tile, x, y))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCell(AppTileData tile, int x, int y)
    {
        var footprint = GetFootprint(tile);

        return x >= footprint.Position.X
            && x < footprint.End.X
            && y >= footprint.Position.Y
            && y < footprint.End.Y;
    }
}
