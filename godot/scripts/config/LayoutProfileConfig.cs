using Godot;
using TileMatcher.Layout;

namespace TileMatcher.Config;

[GlobalClass]
public partial class LayoutProfileConfig : Resource
{
    [Export]
    public string ProfileId { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export]
    public LayoutRuleConfig Rules { get; set; } = null!;

    public LayoutRules ToRuntimeRules()
    {
        return Rules?.ToRuntimeRules() ?? new LayoutRules();
    }
}
