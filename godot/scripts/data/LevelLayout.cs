using System.Collections.Generic;
using System.Linq;

namespace TileMatcher.Data;

public sealed class LevelLayout
{
    public int LevelId { get; init; }

    public List<TileData> Tiles { get; } = [];

    public int LayerCount => Tiles
        .Where(tile => !tile.Removed)
        .Select(tile => tile.GZ)
        .Distinct()
        .Count();
}
