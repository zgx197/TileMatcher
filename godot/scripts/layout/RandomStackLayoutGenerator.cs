using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TileMatcher.Data;
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

        var width = rng.RandiRange(rules.RandomWidthMin, rules.RandomWidthMax);
        var height = rng.RandiRange(rules.RandomHeightMin, rules.RandomHeightMax);
        var bottomLayer = BuildBottomLayer(rng, width, height, rules);

        var layers = new List<List<Vector2I>> { bottomLayer };
        var previousLayer = bottomLayer;
        var targetLayerCount = rng.RandiRange(rules.MinLayerCount, rules.MaxLayerCount);

        for (var z = 1; z < targetLayerCount; z++)
        {
            var candidates = GetSupportedCandidates(previousLayer);
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
            var nextLayer = candidates.Take(targetCount).OrderBy(v => v.Y).ThenBy(v => v.X).ToList();
            if (nextLayer.Count == 0 || nextLayer.Count >= previousLayer.Count)
            {
                break;
            }

            layers.Add(nextLayer);
            previousLayer = nextLayer;
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

                bottomLayer.Add(new Vector2I(x * 2, y * 2));
            }
        }

        if (bottomLayer.Count < rules.MinBottomLayerTileCount)
        {
            bottomLayer.Clear();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    bottomLayer.Add(new Vector2I(x * 2, y * 2));
                }
            }
        }

        return bottomLayer;
    }

    private static List<Vector2I> GetSupportedCandidates(IReadOnlyCollection<Vector2I> lowerLayer)
    {
        var set = lowerLayer.ToHashSet();
        var candidates = new HashSet<Vector2I>();

        foreach (var tile in lowerLayer)
        {
            var candidate = new Vector2I(tile.X + 1, tile.Y + 1);
            if (set.Contains(new Vector2I(candidate.X - 1, candidate.Y - 1))
                && set.Contains(new Vector2I(candidate.X + 1, candidate.Y - 1))
                && set.Contains(new Vector2I(candidate.X - 1, candidate.Y + 1))
                && set.Contains(new Vector2I(candidate.X + 1, candidate.Y + 1)))
            {
                candidates.Add(candidate);
            }
        }

        return candidates.ToList();
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
