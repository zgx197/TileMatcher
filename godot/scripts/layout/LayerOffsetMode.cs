namespace TileMatcher.Layout;

/// <summary>
/// 定义上层相对下层的对齐偏移模式。
/// </summary>
/// <remarks>
/// 它直接决定视觉上更接近“两牌支撑”还是“四牌支撑”。
/// </remarks>
public enum LayerOffsetMode
{
    None = 0,
    HalfX = 1,
    HalfY = 2,
    HalfXY = 3,
}
