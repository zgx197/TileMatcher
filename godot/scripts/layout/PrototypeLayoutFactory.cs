using TileMatcher.Data;

namespace TileMatcher.Layout;

public static class PrototypeLayoutFactory
{
    public static LevelLayout CreateSingleLevelPrototype(int levelId = 1)
    {
        var layout = new LevelLayout { LevelId = levelId };

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
            new TileData { Id = 13, Type = "1D", GX = 0, GY = 6, GZ = 0 },
            new TileData { Id = 14, Type = "3D", GX = 2, GY = 6, GZ = 0 },
            new TileData { Id = 15, Type = "5D", GX = 4, GY = 6, GZ = 0 },
            new TileData { Id = 16, Type = "7D", GX = 6, GY = 6, GZ = 0 },
            new TileData { Id = 17, Type = "P", GX = 1, GY = 1, GZ = 1 },
            new TileData { Id = 18, Type = "C", GX = 3, GY = 1, GZ = 1 },
            new TileData { Id = 19, Type = "F", GX = 5, GY = 1, GZ = 1 },
            new TileData { Id = 20, Type = "G", GX = 1, GY = 3, GZ = 1 },
            new TileData { Id = 21, Type = "R", GX = 3, GY = 3, GZ = 1 },
            new TileData { Id = 22, Type = "2W", GX = 5, GY = 3, GZ = 1 },
            new TileData { Id = 23, Type = "4W", GX = 1, GY = 5, GZ = 1 },
            new TileData { Id = 24, Type = "6W", GX = 3, GY = 5, GZ = 1 },
            new TileData { Id = 25, Type = "8W", GX = 5, GY = 5, GZ = 1 },
            new TileData { Id = 26, Type = "9W", GX = 2, GY = 2, GZ = 2 },
            new TileData { Id = 27, Type = "E", GX = 4, GY = 2, GZ = 2 },
            new TileData { Id = 28, Type = "S", GX = 2, GY = 4, GZ = 2 },
            new TileData { Id = 29, Type = "W", GX = 4, GY = 4, GZ = 2 },
            new TileData { Id = 30, Type = "N", GX = 3, GY = 3, GZ = 3 },
        ]);

        return layout;
    }
}
