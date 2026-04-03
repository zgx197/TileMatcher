namespace TileMatcher.Layout;

public static class GameLayoutProfiles
{
    public const string StandardProfileId = "standard";
    public const string StrictProfileId = "strict";
    public const string WideProfileId = "wide";

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

    public static LayoutRules StrictDebugProfile { get; } = new()
    {
        MinLayerCount = 4,
        MaxLayerCount = 6,
        MinBottomLayerTileCount = 16,
        RequireStrictSupport = true,
        RequireUpperLayerStrictlySmaller = true,
        RandomWidthMin = 5,
        RandomWidthMax = 6,
        RandomHeightMin = 5,
        RandomHeightMax = 7,
        BottomLayerHoleChance = 0.08f,
        GenerationMaxAttempts = 96,
    };

    public static LayoutRules WideDebugProfile { get; } = new()
    {
        MinLayerCount = 4,
        MaxLayerCount = 5,
        MinBottomLayerTileCount = 14,
        RequireStrictSupport = true,
        RequireUpperLayerStrictlySmaller = true,
        RandomWidthMin = 5,
        RandomWidthMax = 6,
        RandomHeightMin = 6,
        RandomHeightMax = 7,
        BottomLayerHoleChance = 0.2f,
        GenerationMaxAttempts = 80,
    };

    public static (string Id, string Name, LayoutRules Rules)[] GetProfiles()
    {
        return
        [
            (StandardProfileId, "标准单关", VitaMahjongSingleLevel),
            (StrictProfileId, "严格高层", StrictDebugProfile),
            (WideProfileId, "宽桌测试", WideDebugProfile),
        ];
    }

    public static LayoutRules GetRulesById(string profileId)
    {
        return profileId switch
        {
            StrictProfileId => StrictDebugProfile,
            WideProfileId => WideDebugProfile,
            _ => VitaMahjongSingleLevel,
        };
    }

    public static string GetDisplayName(string profileId)
    {
        foreach (var profile in GetProfiles())
        {
            if (profile.Id == profileId)
            {
                return profile.Name;
            }
        }

        return "标准单关";
    }
}
