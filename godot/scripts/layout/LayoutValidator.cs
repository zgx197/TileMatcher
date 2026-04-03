using System.Collections.Generic;
using System.Linq;
using TileMatcher.Data;
using TileMatcher.Grid;

namespace TileMatcher.Layout;

public static class LayoutValidator
{
    public static LayoutValidationResult Validate(LevelLayout layout, LayoutRules rules)
    {
        var result = new LayoutValidationResult();
        var activeTiles = layout.Tiles.Where(tile => !tile.Removed).ToList();
        if (activeTiles.Count == 0)
        {
            result.Errors.Add("布局中没有可用麻将。");
            return result;
        }

        var tilesByLayer = activeTiles
            .GroupBy(tile => tile.GZ)
            .OrderBy(group => group.Key)
            .ToList();

        ValidateLayerCount(tilesByLayer, rules, result);
        ValidateBottomLayer(tilesByLayer, rules, result);

        if (rules.RequireUpperLayerStrictlySmaller)
        {
            ValidateLayerShrink(tilesByLayer, result);
        }

        if (rules.RequireStrictSupport)
        {
            ValidateStrictSupport(tilesByLayer, result);
        }

        return result;
    }

    private static void ValidateLayerCount(IReadOnlyList<IGrouping<int, TileData>> tilesByLayer, LayoutRules rules, LayoutValidationResult result)
    {
        if (tilesByLayer.Count < rules.MinLayerCount)
        {
            result.Errors.Add($"层数不足：当前 {tilesByLayer.Count} 层，要求至少 {rules.MinLayerCount} 层。");
        }

        if (tilesByLayer.Count > rules.MaxLayerCount)
        {
            result.Errors.Add($"层数超限：当前 {tilesByLayer.Count} 层，要求最多 {rules.MaxLayerCount} 层。");
        }
    }

    private static void ValidateBottomLayer(IReadOnlyList<IGrouping<int, TileData>> tilesByLayer, LayoutRules rules, LayoutValidationResult result)
    {
        var bottomLayer = tilesByLayer.FirstOrDefault();
        if (bottomLayer is null)
        {
            return;
        }

        if (bottomLayer.Count() < rules.MinBottomLayerTileCount)
        {
            result.Errors.Add($"底层麻将不足：当前 {bottomLayer.Count()} 张，要求至少 {rules.MinBottomLayerTileCount} 张。");
        }
    }

    private static void ValidateLayerShrink(IReadOnlyList<IGrouping<int, TileData>> tilesByLayer, LayoutValidationResult result)
    {
        for (var i = 1; i < tilesByLayer.Count; i++)
        {
            var lowerCount = tilesByLayer[i - 1].Count();
            var currentCount = tilesByLayer[i].Count();
            if (currentCount >= lowerCount)
            {
                result.Errors.Add($"层级收缩不合法：L{tilesByLayer[i].Key} 有 {currentCount} 张，不小于下层 L{tilesByLayer[i - 1].Key} 的 {lowerCount} 张。");
            }
        }
    }

    private static void ValidateStrictSupport(IReadOnlyList<IGrouping<int, TileData>> tilesByLayer, LayoutValidationResult result)
    {
        var layerMap = tilesByLayer.ToDictionary(group => group.Key, group => (IReadOnlyCollection<TileData>)group.ToList());

        foreach (var tile in tilesByLayer
                     .SelectMany(group => group)
                     .Where(tile => tile.GZ > 0)
                     .OrderBy(tile => tile.GZ)
                     .ThenBy(tile => tile.GY)
                     .ThenBy(tile => tile.GX))
        {
            if (!layerMap.TryGetValue(tile.GZ - 1, out var lowerLayerTiles)
                || !GridMath.HasFullSupportFromLowerLayer(tile, lowerLayerTiles))
            {
                result.Errors.Add($"麻将缺少完整四点支撑：Tile#{tile.Id} {tile.Type} at ({tile.GX},{tile.GY},{tile.GZ})。");
            }
        }
    }
}
