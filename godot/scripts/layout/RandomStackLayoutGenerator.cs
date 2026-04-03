using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TileMatcher.Data;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Layout;

public static class RandomStackLayoutGenerator
{
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

        for (var z = 0; z < layers.Count; z++)
        {
            foreach (var position in layers[z])
            {
                layout.Tiles.Add(new AppTileData
                {
                    Id = nextId++,
                    Type = PickTileType(rng),
                    GX = position.X,
                    GY = position.Y,
                    GZ = z,
                    Shape = tileShape,
                });
            }
        }

        return layout;
    }

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

    private static bool DoAnchorsOverlap(Vector2I a, Vector2I b, TileShape tileShape)
    {
        return a.X < b.X + tileShape.WidthUnits
            && a.X + tileShape.WidthUnits > b.X
            && a.Y < b.Y + tileShape.HeightUnits
            && a.Y + tileShape.HeightUnits > b.Y;
    }

    private static int GetFirstAlignedCoordinate(int minValue, int step, int offset)
    {
        var value = offset;
        while (value < minValue)
        {
            value += step;
        }

        return value;
    }

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

    private static string PickTileType(RandomNumberGenerator rng)
    {
        string[] tileTypes =
        [
            "1B", "2B", "3B", "4B", "5B", "6B",
            "1D", "2D", "3D", "4D", "5D", "6D",
            "1W", "2W", "3W", "4W", "5W", "6W",
            "E", "S", "W", "N", "R", "G", "C",
        ];

        return tileTypes[rng.RandiRange(0, tileTypes.Length - 1)];
    }

    private static void Shuffle(RandomNumberGenerator rng, IList<Vector2I> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = rng.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }
}
