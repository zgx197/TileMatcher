using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TileMatcher.Board;
using TileMatcher.Data;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Layout;

/// <summary>
/// 基于当前规则生成随机堆叠布局。
/// 当前版本把“几何布局生成”和“可解牌面分配”合并在同一个入口里，
/// 目标不是追求最强随机性，而是优先保证每一关都有至少一条可通关路径。
/// </summary>
public static class RandomStackLayoutGenerator
{
    /// <summary>
    /// 对同一份几何布局，最多尝试多少次随机移除顺序搜索。
    /// 只要能找到一条完整移空的顺序，就按这条顺序为牌面成对赋值。
    /// </summary>
    private const int SolvableAssignmentMaxAttempts = 96;

    /// <summary>在允许的尝试次数内生成一份合法且可解的布局。</summary>
    public static LevelLayout Generate(int levelId, LayoutRules rules, int? seed = null)
    {
        for (var attempt = 1; attempt <= rules.GenerationMaxAttempts; attempt++)
        {
            var candidate = CreateCandidate(levelId, rules, seed, attempt);
            if (candidate is null)
            {
                continue;
            }

            var validation = LayoutValidator.Validate(candidate, rules);
            if (validation.IsValid)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("未能在限定次数内生成符合业务规则且可通关的布局。");
    }

    /// <summary>
    /// 生成单次候选布局。
    /// 如果几何布局生成成功但找不到可解牌面分配，则返回 null，让外层继续重试。
    /// </summary>
    private static LevelLayout? CreateCandidate(int levelId, LayoutRules rules, int? seed, int attempt)
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

        var generatedTiles = new List<AppTileData>();
        var nextId = 1;
        for (var z = 0; z < layers.Count; z++)
        {
            foreach (var position in layers[z])
            {
                generatedTiles.Add(new AppTileData
                {
                    Id = nextId++,
                    Type = string.Empty,
                    GX = position.X,
                    GY = position.Y,
                    GZ = z,
                    Shape = tileShape,
                });
            }
        }

        // 先得到纯几何布局，再尝试分配一组保证可解的牌面顺序。
        if (!TryAssignSolvablePairs(generatedTiles, rng, out var typeByTileId))
        {
            return null;
        }

        var layout = new LevelLayout { LevelId = levelId };
        foreach (var tile in generatedTiles)
        {
            layout.Tiles.Add(new AppTileData
            {
                Id = tile.Id,
                Type = typeByTileId[tile.Id],
                GX = tile.GX,
                GY = tile.GY,
                GZ = tile.GZ,
                Shape = tile.Shape,
            });
        }

        return layout;
    }

    /// <summary>
    /// 给一份纯几何布局分配可解牌面。
    /// 方法是先随机搜索一条完整的移除顺序，再为顺序中的每一对牌赋同一种类型。
    /// </summary>
    private static bool TryAssignSolvablePairs(
        IReadOnlyList<AppTileData> tiles,
        RandomNumberGenerator rng,
        out Dictionary<int, string> typeByTileId)
    {
        typeByTileId = [];

        for (var attempt = 0; attempt < SolvableAssignmentMaxAttempts; attempt++)
        {
            var simulationTiles = tiles
                .Select(tile => new AppTileData
                {
                    Id = tile.Id,
                    Type = string.Empty,
                    GX = tile.GX,
                    GY = tile.GY,
                    GZ = tile.GZ,
                    Shape = tile.Shape,
                })
                .ToList();

            var removedPairs = new List<(int FirstId, int SecondId)>();
            // 使用与运行时一致的“可参与消除”判定来做纯数据模拟，
            // 让生成结果和真实对局规则尽量保持一致。
            if (!TryBuildRemovalSequence(simulationTiles, rng, removedPairs))
            {
                continue;
            }

            var pairTypes = TileTypeDeckBuilder.BuildPairTypeSequence(removedPairs.Count, rng);
            for (var i = 0; i < removedPairs.Count; i++)
            {
                var pairType = pairTypes[i];
                typeByTileId[removedPairs[i].FirstId] = pairType;
                typeByTileId[removedPairs[i].SecondId] = pairType;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 在当前布局上构造一条完整的可移除顺序。
    /// 每一步只要求找到两张当前自由牌即可，因为牌面还未分配，
    /// 后续会让这两张牌拥有相同类型。
    /// </summary>
    private static bool TryBuildRemovalSequence(
        List<AppTileData> simulationTiles,
        RandomNumberGenerator rng,
        List<(int FirstId, int SecondId)> removedPairs)
    {
        while (simulationTiles.Count > 0)
        {
            var activeTiles = simulationTiles
                .Where(tile => !tile.Removed)
                .ToList();
            var movableTiles = activeTiles
                .Where(tile => TileInteractionRules.CanParticipateInMatch(tile, activeTiles))
                .ToList();

            if (movableTiles.Count < 2)
            {
                // 只要某一步找不到两张自由牌，这条顺序就作废，交给外层重新随机尝试。
                return false;
            }

            Shuffle(rng, movableTiles);
            var first = movableTiles[0];
            var second = movableTiles[1];
            removedPairs.Add((first.Id, second.Id));
            simulationTiles.RemoveAll(tile => tile.Id == first.Id || tile.Id == second.Id);
        }

        return true;
    }

    /// <summary>
    /// 构造底层牌阵。
    /// 当前底层仍使用按步长对齐的规则网格，而不是任意放置。
    /// </summary>
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
    private static void Shuffle<T>(RandomNumberGenerator rng, IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = rng.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }
}
