using System.Collections.Generic;

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
            new TileData { Id = 5, Type = "2D", GX = 1, GY = 2, GZ = 0 },
            new TileData { Id = 6, Type = "4D", GX = 3, GY = 2, GZ = 0 },
            new TileData { Id = 7, Type = "6D", GX = 5, GY = 2, GZ = 0 },
            new TileData { Id = 8, Type = "8D", GX = 7, GY = 2, GZ = 0 },
            new TileData { Id = 9, Type = "E", GX = 0, GY = 4, GZ = 0 },
            new TileData { Id = 10, Type = "S", GX = 2, GY = 4, GZ = 0 },
            new TileData { Id = 11, Type = "W", GX = 4, GY = 4, GZ = 0 },
            new TileData { Id = 12, Type = "N", GX = 6, GY = 4, GZ = 0 },
            new TileData { Id = 13, Type = "P", GX = 1, GY = 1, GZ = 1 },
            new TileData { Id = 14, Type = "C", GX = 3, GY = 1, GZ = 1 },
            new TileData { Id = 15, Type = "F", GX = 5, GY = 1, GZ = 1 },
            new TileData { Id = 16, Type = "G", GX = 2, GY = 3, GZ = 1 },
            new TileData { Id = 17, Type = "R", GX = 4, GY = 3, GZ = 1 },
            new TileData { Id = 18, Type = "9W", GX = 3, GY = 2, GZ = 2 },
        ]);

        return layout;
    }
}
