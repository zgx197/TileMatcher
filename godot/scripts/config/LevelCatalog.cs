using Godot;

namespace TileMatcher.Config;

[GlobalClass]
public partial class LevelCatalog : Resource
{
    [Export]
    public int DefaultLevelNumber { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<LevelConfig> Levels { get; set; } = [];

    public LevelConfig? ResolveLevel(int levelNumber)
    {
        foreach (var level in Levels)
        {
            if (level is not null
                && !level.UseAsOfflineCatalogTemplate
                && level.LevelNumber == levelNumber)
            {
                return level;
            }
        }

        foreach (var level in Levels)
        {
            if (level is not null
                && level.UseAsOfflineCatalogTemplate
                && level.LayoutSourceMode == LevelLayoutSourceMode.OfflineJson
                && !string.IsNullOrWhiteSpace(level.OfflineCatalogJsonPath))
            {
                return level.CreateResolvedCopy(levelNumber);
            }
        }

        return null;
    }

    public LevelConfig? ResolveLevelOrFallback(int levelNumber)
    {
        return ResolveLevel(levelNumber) ?? ResolveLevel(DefaultLevelNumber);
    }
}
