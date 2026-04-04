using Godot;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// 关卡目录资源。
/// 负责维护可用关卡配置列表，并提供按关卡号解析与默认回退能力。
/// </summary>
public partial class LevelCatalog : Resource
{
    /// <summary>当目标关卡缺失时使用的默认回退关卡号。</summary>
    [Export]
    public int DefaultLevelNumber { get; set; } = 1;

    /// <summary>目录中显式配置的全部关卡条目。</summary>
    [Export]
    public Godot.Collections.Array<LevelConfig> Levels { get; set; } = [];

    /// <summary>按关卡号解析配置，必要时允许使用离线目录模板动态生成。</summary>
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

    /// <summary>按关卡号解析配置，失败时再回退到默认关卡。</summary>
    public LevelConfig? ResolveLevelOrFallback(int levelNumber)
    {
        return ResolveLevel(levelNumber) ?? ResolveLevel(DefaultLevelNumber);
    }
}
