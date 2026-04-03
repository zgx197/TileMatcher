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
}
