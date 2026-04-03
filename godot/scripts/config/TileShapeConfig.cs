using Godot;
using TileMatcher.Data;

namespace TileMatcher.Config;

[GlobalClass]
public partial class TileShapeConfig : Resource
{
    // 牌形是通用堆叠框架的最小可配置几何单元。
    [Export]
    public int WidthUnits { get; set; } = 4;

    [Export]
    public int HeightUnits { get; set; } = 6;

    public TileShape ToRuntimeShape()
    {
        return new TileShape
        {
            WidthUnits = WidthUnits,
            HeightUnits = HeightUnits,
        };
    }
}
