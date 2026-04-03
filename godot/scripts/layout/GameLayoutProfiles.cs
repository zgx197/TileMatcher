namespace TileMatcher.Layout;

public static class GameLayoutProfiles
{
    public static LayoutRules VitaMahjongSingleLevel { get; } = new()
    {
        MinLayerCount = 4,
        MaxLayerCount = 5,
        MinBottomLayerTileCount = 12,
        RequireStrictSupport = true,
        RequireUpperLayerStrictlySmaller = true,
        RandomWidthMin = 4,
        RandomWidthMax = 5,
        RandomHeightMin = 5,
        RandomHeightMax = 6,
        BottomLayerHoleChance = 0.16f,
        GenerationMaxAttempts = 64,
    };
}
