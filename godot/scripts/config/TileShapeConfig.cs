using Godot;
using TileMatcher.Data;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// Godot 侧可编辑的牌形配置资源。
/// </summary>
/// <remarks>
/// 它对应 Unity 里的 ScriptableObject 思路，让牌形尺寸可以直接在 Inspector 中调整。
/// </remarks>
public partial class TileShapeConfig : Resource
{
    /// <summary>牌形宽度，单位是逻辑微单元。</summary>
    [Export]
    public int WidthUnits { get; set; } = 4;

    /// <summary>牌形高度，单位是逻辑微单元。</summary>
    [Export]
    public int HeightUnits { get; set; } = 6;

    /// <summary>转换成运行时逻辑层使用的 TileShape。</summary>
    public TileShape ToRuntimeShape()
    {
        return new TileShape
        {
            WidthUnits = WidthUnits,
            HeightUnits = HeightUnits,
        };
    }
}
