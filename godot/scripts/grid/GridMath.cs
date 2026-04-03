using System.Collections.Generic;
using System.Linq;
using Godot;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Grid;

/// <summary>
/// 逻辑网格与世界空间之间的几何计算工具集。
/// </summary>
/// <remarks>
/// 这里承载第二代堆叠框架的几何核心：
/// - footprint 计算
/// - 同层重叠判定
/// - 世界坐标换算
/// - 上下层覆盖关系判定
/// 
/// 这些函数必须尽量保持纯计算，不依赖节点或场景状态。
/// </remarks>
public static class GridMath
{
    /// <summary>返回一张牌在逻辑平面上的矩形 footprint。</summary>
    public static Rect2I GetFootprint(AppTileData tile)
    {
        return new Rect2I(tile.GX, tile.GY, tile.FootprintWidth, tile.FootprintHeight);
    }

    /// <summary>
    /// 判断两张牌在二维投影上是否发生重叠。
    /// </summary>
    /// <remarks>
    /// 这里只看 XY，不看层级。
    /// 它既可以用于同层非法重叠检查，也可以用于层间投影分析。
    /// </remarks>
    public static bool OverlapsXY(AppTileData a, AppTileData b)
    {
        var rectA = GetFootprint(a);
        var rectB = GetFootprint(b);

        return rectA.Position.X < rectB.End.X
            && rectA.End.X > rectB.Position.X
            && rectA.Position.Y < rectB.End.Y
            && rectA.End.Y > rectB.Position.Y;
    }

    /// <summary>
    /// 将逻辑网格坐标映射到世界空间左上角位置。
    /// </summary>
    public static Vector2 GridToWorld(int gx, int gy, int gz, Vector2 boardOrigin)
    {
        return boardOrigin
            + new Vector2(gx * GridConfig.CellWidth, gy * GridConfig.CellHeight)
            + GridConfig.LayerVisualOffset * gz;
    }

    /// <summary>根据牌的逻辑 footprint 计算它的世界空间绘制尺寸。</summary>
    public static Vector2 GetWorldSize(AppTileData tile)
    {
        return new Vector2(
            tile.FootprintWidth * GridConfig.CellWidth,
            tile.FootprintHeight * GridConfig.CellHeight);
    }

    /// <summary>
    /// 判断某张牌上方是否还有任何投影重叠的牌。
    /// </summary>
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

    /// <summary>
    /// 判断上层牌的整个底面是否被下一层完整覆盖。
    /// </summary>
    /// <remarks>
    /// 这是第二代框架里最关键的几何规则：
    /// 逐个微单元检查底面投影是否全部被下一层 footprint union 覆盖。
    /// </remarks>
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

    /// <summary>
    /// 计算所有未移除牌在世界空间中的整体包围盒。
    /// </summary>
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

    /// <summary>判断某个逻辑微单元是否被任意一张牌覆盖。</summary>
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

    /// <summary>判断某张牌的 footprint 是否包含指定微单元。</summary>
    private static bool ContainsCell(AppTileData tile, int x, int y)
    {
        var footprint = GetFootprint(tile);

        return x >= footprint.Position.X
            && x < footprint.End.X
            && y >= footprint.Position.Y
            && y < footprint.End.Y;
    }
}
