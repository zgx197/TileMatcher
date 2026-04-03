namespace TileMatcher.Data;

public sealed class TileData
{
    public int Id { get; init; }

    public string Type { get; init; } = string.Empty;

    public int GX { get; init; }

    public int GY { get; init; }

    public int GZ { get; init; }

    public TileShape Shape { get; init; } = TileShape.StandardMahjong;

    public int FootprintWidth => Shape.WidthUnits;

    public int FootprintHeight => Shape.HeightUnits;

    public bool Removed { get; set; }

    public bool Movable { get; set; }
}
