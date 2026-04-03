using Godot;

namespace TileMatcher.Config;

[GlobalClass]
public partial class LayoutProfileCatalog : Resource
{
    // 档案目录负责给调试 UI 和运行时提供同一份 profile 列表。
    [Export]
    public string DefaultProfileId { get; set; } = string.Empty;

    [Export]
    public Godot.Collections.Array<LayoutProfileConfig> Profiles { get; set; } = [];
}
