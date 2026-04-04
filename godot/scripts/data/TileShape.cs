namespace TileMatcher.Data;

/// <summary>
/// 牌在逻辑网格中的几何形状定义。
/// </summary>
/// <remarks>
/// 当前只支持矩形 footprint，因此只需要宽高两个参数。
/// 如果未来要支持非矩形 shape，这里会升级成更通用的数据结构。
/// </remarks>
public sealed class TileShape
{
    /// <summary>
    /// 项目默认标准牌的逻辑尺寸。
    /// </summary>
    /// <remarks>
    /// 4x6 代表逻辑微单元尺寸，而不是像素尺寸。
    /// </remarks>
    public static TileShape StandardTile { get; } = new()
    {
        WidthUnits = 4,
        HeightUnits = 6,
    };

    /// <summary>footprint 宽度，单位是逻辑微单元。</summary>
    public int WidthUnits { get; init; }

    /// <summary>footprint 高度，单位是逻辑微单元。</summary>
    public int HeightUnits { get; init; }

    /// <summary>
    /// 生成一份值相同的新实例。
    /// </summary>
    public TileShape Clone()
    {
        return new TileShape
        {
            WidthUnits = WidthUnits,
            HeightUnits = HeightUnits,
        };
    }
}
