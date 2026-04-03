using Godot;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// 关卡目录资源。
/// 外围流程只关心“给我第 N 关怎么开”，因此目录资源负责集中持有关卡列表和默认回退项。
/// </summary>
public partial class LevelCatalog : Resource
{
    /// <summary>当请求的关卡编号未配置时，允许回退到的默认关卡编号。</summary>
    [Export]
    public int DefaultLevelNumber { get; set; } = 1;

    /// <summary>当前可用的全部关卡配置。</summary>
    [Export]
    public Godot.Collections.Array<LevelConfig> Levels { get; set; } = [];
}
