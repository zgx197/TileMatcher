using System.Collections.Generic;
using System.Linq;

namespace TileMatcher.Data;

/// <summary>
/// 一整副当前关卡布局的运行时数据容器。
/// </summary>
/// <remarks>
/// 它只负责聚合某次生成结果或原型关卡，不负责生成和校验。
/// </remarks>
public sealed class LevelLayout
{
    /// <summary>关卡编号，当前主要用于调试和未来关卡资源扩展。</summary>
    public int LevelId { get; init; }

    /// <summary>当前布局中的全部牌数据，包含已移除与未移除状态。</summary>
    public List<TileData> Tiles { get; } = [];

    /// <summary>
    /// 当前仍然存在的层数统计。
    /// </summary>
    /// <remarks>
    /// 这里按未移除的牌来计算，而不是直接看最大层号，
    /// 这样后续整层消失时统计仍然正确。
    /// </remarks>
    public int LayerCount => Tiles
        .Where(tile => !tile.Removed)
        .Select(tile => tile.GZ)
        .Distinct()
        .Count();
}
