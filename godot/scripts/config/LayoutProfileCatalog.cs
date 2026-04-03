using Godot;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// 规则档案目录资源。
/// </summary>
/// <remarks>
/// 它负责给调试 UI 和运行时提供同一份 profile 列表。
/// 目录资源本身不承载业务逻辑，只负责约定：
/// - 哪些档案可用
/// - 默认使用哪一份档案
/// </remarks>
public partial class LayoutProfileCatalog : Resource
{
    /// <summary>默认启用的档案 id。</summary>
    [Export]
    public string DefaultProfileId { get; set; } = string.Empty;

    /// <summary>全部可用规则档案。</summary>
    [Export]
    public Godot.Collections.Array<LayoutProfileConfig> Profiles { get; set; } = [];
}
