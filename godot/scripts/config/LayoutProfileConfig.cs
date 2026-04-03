using Godot;
using TileMatcher.Layout;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// 一份完整的规则档案。
/// </summary>
/// <remarks>
/// profile 是给“调试切换 / 关卡引用 / 将来玩法模式切换”使用的稳定配置单元。
/// 它本身不直接参与生成，只负责把“名字 + 规则”绑定在一起。
/// </remarks>
public partial class LayoutProfileConfig : Resource
{
    /// <summary>内部稳定 id，用于代码选择和持久化。</summary>
    [Export]
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>给用户和调试 UI 展示的名称。</summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>这份档案引用的规则资源。</summary>
    [Export]
    public LayoutRuleConfig Rules { get; set; } = null!;

    /// <summary>
    /// 转换成运行时使用的 LayoutRules。
    /// </summary>
    /// <remarks>
    /// 这里提供一个统一入口，避免上层代码直接去摸 Rules 并各自转换。
    /// </remarks>
    public LayoutRules ToRuntimeRules()
    {
        return Rules?.ToRuntimeRules() ?? new LayoutRules();
    }
}
