namespace TileMatcher.Data;

public sealed class TileShape
{
    public static TileShape StandardMahjong { get; } = new()
    {
        WidthUnits = 4,
        HeightUnits = 6,
    };

    public int WidthUnits { get; init; }

    public int HeightUnits { get; init; }

    public TileShape Clone()
    {
        return new TileShape
        {
            WidthUnits = WidthUnits,
            HeightUnits = HeightUnits,
        };
    }
}
