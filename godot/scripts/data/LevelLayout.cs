using System.Collections.Generic;
using System.Linq;
using Godot;
using TileMatcher.Grid;

namespace TileMatcher.Data;

public sealed class LevelLayout
{
    public int LevelId { get; init; }

    public List<TileData> Tiles { get; } = [];

    public static LevelLayout CreatePrototype()
    {
        var layout = new LevelLayout { LevelId = 1 };

        layout.Tiles.AddRange(
        [
            new TileData { Id = 1, Type = "1B", GX = 0, GY = 0, GZ = 0 },
            new TileData { Id = 2, Type = "3B", GX = 2, GY = 0, GZ = 0 },
            new TileData { Id = 3, Type = "5W", GX = 4, GY = 0, GZ = 0 },
            new TileData { Id = 4, Type = "7T", GX = 6, GY = 0, GZ = 0 },
            new TileData { Id = 5, Type = "2D", GX = 0, GY = 2, GZ = 0 },
            new TileData { Id = 6, Type = "4D", GX = 2, GY = 2, GZ = 0 },
            new TileData { Id = 7, Type = "6D", GX = 4, GY = 2, GZ = 0 },
            new TileData { Id = 8, Type = "8D", GX = 6, GY = 2, GZ = 0 },
            new TileData { Id = 9, Type = "E", GX = 0, GY = 4, GZ = 0 },
            new TileData { Id = 10, Type = "S", GX = 2, GY = 4, GZ = 0 },
            new TileData { Id = 11, Type = "W", GX = 4, GY = 4, GZ = 0 },
            new TileData { Id = 12, Type = "N", GX = 6, GY = 4, GZ = 0 },
            new TileData { Id = 13, Type = "P", GX = 1, GY = 1, GZ = 1 },
            new TileData { Id = 14, Type = "C", GX = 3, GY = 1, GZ = 1 },
            new TileData { Id = 15, Type = "F", GX = 5, GY = 1, GZ = 1 },
            new TileData { Id = 16, Type = "G", GX = 1, GY = 3, GZ = 1 },
            new TileData { Id = 17, Type = "R", GX = 3, GY = 3, GZ = 1 },
            new TileData { Id = 18, Type = "9W", GX = 2, GY = 2, GZ = 2 },
        ]);

        EnsureStrictSupport(layout);
        return layout;
    }

    public static LevelLayout CreateRandomStack(int levelId = 1, int? seed = null)
    {
        var rng = new RandomNumberGenerator();
        if (seed.HasValue)
        {
            rng.Seed = (ulong)seed.Value;
        }
        else
        {
            rng.Randomize();
        }

        var layout = new LevelLayout { LevelId = levelId };
        var nextId = 1;

        var width = rng.RandiRange(4, 5);
        var height = rng.RandiRange(5, 6);
        var bottomLayer = new List<Vector2I>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isCorner = (x == 0 || x == width - 1) && (y == 0 || y == height - 1);
                if (!isCorner && rng.Randf() < 0.16f)
                {
                    continue;
                }

                bottomLayer.Add(new Vector2I(x * 2, y * 2));
            }
        }

        if (bottomLayer.Count < 12)
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

        var layers = new List<List<Vector2I>> { bottomLayer };
        var previousLayer = bottomLayer;
        var maxLayers = rng.RandiRange(3, 5);

        for (var z = 1; z < maxLayers; z++)
        {
            var candidates = GetSupportedCandidates(previousLayer);
            if (candidates.Count == 0)
            {
                break;
            }

            var targetCount = Mathf.Clamp(
                rng.RandiRange(Mathf.Max(1, candidates.Count / 2), candidates.Count),
                1,
                Mathf.Max(1, previousLayer.Count - 1));

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
                layout.Tiles.Add(new TileData
                {
                    Id = nextId++,
                    Type = PickTileType(rng),
                    GX = position.X,
                    GY = position.Y,
                    GZ = z,
                });
            }
        }

        EnsureStrictSupport(layout);
        return layout;
    }

    public IReadOnlyList<string> GetStrictSupportIssues()
    {
        var tilesByLayer = Tiles
            .Where(tile => !tile.Removed)
            .GroupBy(tile => tile.GZ)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<TileData>)group.ToList());

        var issues = new List<string>();
        foreach (var tile in Tiles.Where(tile => !tile.Removed && tile.GZ > 0).OrderBy(tile => tile.GZ).ThenBy(tile => tile.GY).ThenBy(tile => tile.GX))
        {
            if (!tilesByLayer.TryGetValue(tile.GZ - 1, out var lowerLayerTiles)
                || !GridMath.HasFullSupportFromLowerLayer(tile, lowerLayerTiles))
            {
                issues.Add($"Tile#{tile.Id} {tile.Type} at ({tile.GX},{tile.GY},{tile.GZ}) 缺少完整四点支撑");
            }
        }

        return issues;
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

    private static void EnsureStrictSupport(LevelLayout layout)
    {
        var issues = layout.GetStrictSupportIssues();
        if (issues.Count == 0)
        {
            return;
        }

        throw new System.InvalidOperationException($"布局违反四点支撑规则: {string.Join(" | ", issues)}");
    }
}
