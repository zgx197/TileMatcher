using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TileMatcher.Data;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Layout;

/// <summary>
/// 基于当前规则生成随机堆叠布局。
/// </summary>
/// <remarks>
/// 当前采用“自底向上 + 多次尝试 + 校验兜底”的策略。
/// 这不是最终关卡生成器，但非常适合快速迭代阶段。
/// </remarks>
public static class RandomStackLayoutGenerator
{
    /// <summary>在允许的尝试次数内生成一份合法布局。</summary>
    public static LevelLayout Generate(int levelId, LayoutRules rules, int? seed = null)
    {
        for (var attempt = 1; attempt <= rules.GenerationMaxAttempts; attempt++)
        {
            var candidate = CreateCandidate(levelId, rules, seed, attempt);
            var validation = LayoutValidator.Validate(candidate, rules);
            if (validation.IsValid)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("未能在限定次数内生成符合业务规则的布局。");
    }

    /// <summary>
    /// 生成单次候选布局。
    /// </summary>
    /// <remarks>
    /// 单次候选不保证一定合法，所以外层还需要 Validate 兜底。
    /// </remarks>
    private static LevelLayout CreateCandidate(int levelId, LayoutRules rules, int? seed, int attempt)
    {
        var rng = new RandomNumberGenerator();
        if (seed.HasValue)
        {
            rng.Seed = (ulong)(seed.Value + attempt - 1);
        }
        else
        {
            rng.Randomize();
        }

        var layout = new LevelLayout { LevelId = levelId };
        var nextId = 1;
        var tileShape = rules.CreateTileShape();

        var width = rng.RandiRange(rules.RandomWidthMin, rules.RandomWidthMax);
        var height = rng.RandiRange(rules.RandomHeightMin, rules.RandomHeightMax);
        var bottomLayer = BuildBottomLayer(rng, width, height, rules);

        var layers = new List<List<Vector2I>> { bottomLayer };
        var previousLayer = CreateTemporaryLayer(bottomLayer, 0, tileShape);
        var targetLayerCount = rng.RandiRange(rules.MinLayerCount, rules.MaxLayerCount);

        for (var z = 1; z < targetLayerCount; z++)
        {
            var layerOffset = ResolveLayerOffset(z, rules, tileShape);
            var candidates = GetSupportedCandidates(previousLayer, tileShape, layerOffset);
            if (candidates.Count == 0)
            {
                break;
            }

            var minCount = Mathf.Max(1, Math.Min(candidates.Count, Mathf.Max(1, previousLayer.Count / 2)));
            var maxCount = Mathf.Max(1, Math.Min(candidates.Count, previousLayer.Count - 1));
            if (maxCount < minCount)
            {
                break;
            }

            var targetCount = rng.RandiRange(minCount, maxCount);
            Shuffle(rng, candidates);
            var nextLayer = SelectNonOverlappingCandidates(candidates, targetCount, tileShape)
                .OrderBy(v => v.Y)
                .ThenBy(v => v.X)
                .ToList();
            if (nextLayer.Count == 0 || nextLayer.Count >= previousLayer.Count)
            {
                break;
            }

            layers.Add(nextLayer);
            previousLayer = CreateTemporaryLayer(nextLayer, z, tileShape);
        }

        var pairDeck = TileTypeDeckBuilder.BuildShuffledPairDeck(layers.Sum(layer => layer.Count), rng);
        var typeIndex = 0;

        for (var z = 0; z < layers.Count; z++)
        {
            foreach (var position in layers[z])
            {
                layout.Tiles.Add(new AppTileData
                {
                    Id = nextId++,
                    Type = pairDeck[typeIndex++],
                    GX = position.X,
                    GY = position.Y,
                    GZ = z,
                    Shape = tileShape,
                });
            }
        }

        return layout;
    }

    /// <summary>
    /// 构造底层牌阵。
    /// </summary>
    /// <remarks>
    /// 当前底层仍然使用按步长对齐的规则网格，而不是任意放置。
    /// </remarks>
    private static List<Vector2I> BuildBottomLayer(RandomNumberGenerator rng, int width, int height, LayoutRules rules)
    {
        var bottomLayer = new List<Vector2I>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isCorner = (x == 0 || x == width - 1) && (y == 0 || y == height - 1);
                if (!isCorner && rng.Randf() < rules.BottomLayerHoleChance)
                {
                    continue;
                }

                bottomLayer.Add(new Vector2I(x * rules.BottomLayerStepX, y * rules.BottomLayerStepY));
            }
        }

        if (bottomLayer.Count < rules.MinBottomLayerTileCount)
        {
            bottomLayer.Clear();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    bottomLayer.Add(new Vector2I(x * rules.BottomLayerStepX, y * rules.BottomLayerStepY));
                }
            }
        }

        return bottomLayer;
    }

    /// <summary>
    /// 根据下一层已有牌，枚举当前层全部合法候选点。
    /// </summary>
    private static List<Vector2I> GetSupportedCandidates(
        IReadOnlyCollection<AppTileData> lowerLayer,
        TileShape tileShape,
        Vector2I layerOffset)
    {
        var candidates = new HashSet<Vector2I>();
        var minX = lowerLayer.Min(tile => tile.GX);
        var minY = lowerLayer.Min(tile => tile.GY);
        var maxX = lowerLayer.Max(tile => tile.GX + tile.FootprintWidth);
        var maxY = lowerLayer.Max(tile => tile.GY + tile.FootprintHeight);
        var startX = GetFirstAlignedCoordinate(minX, tileShape.WidthUnits, layerOffset.X);
        var startY = GetFirstAlignedCoordinate(minY, tileShape.HeightUnits, layerOffset.Y);

        for (var y = startY; y <= maxY - tileShape.HeightUnits; y += tileShape.HeightUnits)
        {
            for (var x = startX; x <= maxX - tileShape.WidthUnits; x += tileShape.WidthUnits)
            {
                var candidate = new AppTileData
                {
                    GX = x,
                    GY = y,
                    GZ = lowerLayer.First().GZ + 1,
                    Shape = tileShape,
                };

                if (GridMath.HasFullSupportFromLowerLayer(candidate, lowerLayer))
                {
                    candidates.Add(new Vector2I(x, y));
                }
            }
        }

        return candidates.ToList();
    }

    /// <summary>
    /// 从候选点中挑选一组互不重叠的牌。
    /// </summary>
    private static List<Vector2I> SelectNonOverlappingCandidates(
        IReadOnlyList<Vector2I> candidates,
        int targetCount,
        TileShape tileShape)
    {
        var selected = new List<Vector2I>();

        foreach (var candidate in candidates)
        {
            if (selected.Count >= targetCount)
            {
                break;
            }

            if (selected.All(existing => !DoAnchorsOverlap(existing, candidate, tileShape)))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    /// <summary>判断两个锚点在给定牌形下是否会产生同层重叠。</summary>
    private static bool DoAnchorsOverlap(Vector2I a, Vector2I b, TileShape tileShape)
    {
        return a.X < b.X + tileShape.WidthUnits
            && a.X + tileShape.WidthUnits > b.X
            && a.Y < b.Y + tileShape.HeightUnits
            && a.Y + tileShape.HeightUnits > b.Y;
    }

    /// <summary>计算不小于 minValue 的第一个对齐坐标。</summary>
    private static int GetFirstAlignedCoordinate(int minValue, int step, int offset)
    {
        var value = offset;
        while (value < minValue)
        {
            value += step;
        }

        return value;
    }

    /// <summary>
    /// 根据规则计算某层相对下层的对齐偏移。
    /// </summary>
    /// <remarks>
    /// 当前约定偶数层回到对齐基线，奇数层应用配置的半步偏移。
    /// </remarks>
    private static Vector2I ResolveLayerOffset(int layer, LayoutRules rules, TileShape tileShape)
    {
        if (layer % 2 == 0)
        {
            return Vector2I.Zero;
        }

        var halfX = tileShape.WidthUnits / 2;
        var halfY = tileShape.HeightUnits / 2;

        return rules.UpperLayerOffsetMode switch
        {
            LayerOffsetMode.HalfX => new Vector2I(halfX, 0),
            LayerOffsetMode.HalfY => new Vector2I(0, halfY),
            LayerOffsetMode.HalfXY => new Vector2I(halfX, halfY),
            _ => Vector2I.Zero,
        };
    }

    /// <summary>
    /// 把一层锚点列表包装成临时 TileData 集合。
    /// </summary>
    private static List<AppTileData> CreateTemporaryLayer(IEnumerable<Vector2I> anchors, int layer, TileShape tileShape)
    {
        return anchors
            .Select(anchor => new AppTileData
            {
                GX = anchor.X,
                GY = anchor.Y,
                GZ = layer,
                Shape = tileShape,
            })
            .ToList();
    }

    /// <summary>原地打乱列表。</summary>
    private static void Shuffle(RandomNumberGenerator rng, IList<Vector2I> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = rng.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }
}
