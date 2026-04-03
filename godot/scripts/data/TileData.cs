namespace TileMatcher.Data;

public sealed class TileData
{
    public int Id { get; init; }

    public string Type { get; init; } = string.Empty;

    public int GX { get; init; }

    public int GY { get; init; }

    public int GZ { get; init; }

    public bool Removed { get; set; }

    public bool Movable { get; set; }
}
