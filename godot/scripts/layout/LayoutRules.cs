using TileMatcher.Data;
using TileMatcher.Grid;

namespace TileMatcher.Layout;

public sealed class LayoutRules
{
    public int MinLayerCount { get; init; } = 4;

    public int MaxLayerCount { get; init; } = 5;

    public int MinBottomLayerTileCount { get; init; } = 12;

    public bool RequireStrictSupport { get; init; } = true;

    public bool RequireUpperLayerStrictlySmaller { get; init; } = true;

    public int RandomWidthMin { get; init; } = 4;

    public int RandomWidthMax { get; init; } = 5;

    public int RandomHeightMin { get; init; } = 5;

    public int RandomHeightMax { get; init; } = 6;

    public float BottomLayerHoleChance { get; init; } = 0.16f;

    public int GenerationMaxAttempts { get; init; } = 48;

    public int TileWidthUnits { get; init; } = GridConfig.DefaultFootprintWidthUnits;

    public int TileHeightUnits { get; init; } = GridConfig.DefaultFootprintHeightUnits;

    public int BottomLayerStepX { get; init; } = GridConfig.BottomLayerStepX;

    public int BottomLayerStepY { get; init; } = GridConfig.BottomLayerStepY;

    public LayerOffsetMode UpperLayerOffsetMode { get; init; } = LayerOffsetMode.HalfXY;

    public TileShape CreateTileShape()
    {
        return new TileShape
        {
            WidthUnits = TileWidthUnits,
            HeightUnits = TileHeightUnits,
        };
    }

    public string GetOffsetModeDisplayName()
    {
        return UpperLayerOffsetMode switch
        {
            LayerOffsetMode.HalfX => "半宽",
            LayerOffsetMode.HalfY => "半高",
            LayerOffsetMode.HalfXY => "半宽+半高",
            _ => "对齐",
        };
    }
}
