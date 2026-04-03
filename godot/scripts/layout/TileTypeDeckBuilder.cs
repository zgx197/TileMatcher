using System.Collections.Generic;
using Godot;

namespace TileMatcher.Layout;

/// <summary>
/// 负责为布局结果分配可用于匹配消除的牌面类型。
/// </summary>
/// <remarks>
/// 当前阶段先解决一个非常具体的问题：
/// - 牌桌已经支持点击配对和最小拖拽消除
/// - 但如果生成出来的牌面大多互不相同，就很难验证“碰撞后消除”这条链路
///
/// 因此这里统一提供“按对生成”的牌面分配器：
/// - 随机布局默认拿到一副被打乱的成对牌面序列
/// - 固定原型也使用一份可读性更强的成对序列
///
/// 这一层仍然只是临时调试阶段的数据设计，不等于最终正式关卡的牌组设计。
/// 后续如果要支持：
/// - 花牌特殊规则
/// - 多套视觉资源共用同一个匹配组
/// - 保证可解的正式洗牌算法
/// 可以在这里继续升级，而不必改动棋盘交互层。
/// </remarks>
public static class TileTypeDeckBuilder
{
    /// <summary>
    /// 当前可参与普通配对的基础牌面池。
    /// </summary>
    /// <remarks>
    /// 当前先维持字符串类型，兼容现有 `TileData.Type`、`TileView` 文本显示和匹配逻辑。
    /// 等后续引入正式美术时，再考虑拆分成：
    /// - 匹配 key
    /// - 显示资源 id
    /// </remarks>
    private static readonly string[] MatchableTypes =
    [
        "1B", "2B", "3B", "4B", "5B", "6B", "7B", "8B", "9B",
        "1D", "2D", "3D", "4D", "5D", "6D", "7D", "8D", "9D",
        "1W", "2W", "3W", "4W", "5W", "6W", "7W", "8W", "9W",
        "E", "S", "W", "N", "R", "G", "C",
    ];

    /// <summary>
    /// 为固定原型生成一份可读性较强的成对牌面。
    /// </summary>
    /// <remarks>
    /// 固定原型的目标是“稳定复现”和“方便肉眼观察”，
    /// 因此这里不做随机洗牌，而是按固定顺序给出成对数据。
    /// </remarks>
    public static IReadOnlyList<string> BuildPrototypePairDeck(int tileCount)
    {
        var deck = new List<string>(tileCount);
        var pairCount = tileCount / 2;

        for (var i = 0; i < pairCount; i++)
        {
            var type = MatchableTypes[i % MatchableTypes.Length];
            deck.Add(type);
            deck.Add(type);
        }

        if (tileCount % 2 != 0)
        {
            deck.Add(MatchableTypes[0]);
        }

        return deck;
    }

    /// <summary>
    /// 为随机布局生成一份打乱后的成对牌面序列。
    /// </summary>
    public static IReadOnlyList<string> BuildShuffledPairDeck(int tileCount, RandomNumberGenerator rng)
    {
        var deck = new List<string>(tileCount);
        var pairCount = tileCount / 2;

        for (var i = 0; i < pairCount; i++)
        {
            var type = MatchableTypes[rng.RandiRange(0, MatchableTypes.Length - 1)];
            deck.Add(type);
            deck.Add(type);
        }

        // 当前布局生成器还没有强制保证总牌数一定为偶数。
        // 若出现奇数张牌，先补一张重复类型兜底，让调试不被完全阻塞。
        if (tileCount % 2 != 0)
        {
            var extraType = MatchableTypes[rng.RandiRange(0, MatchableTypes.Length - 1)];
            deck.Add(extraType);
            GD.PushWarning($"[TileTypeDeckBuilder] 当前布局牌数为奇数 {tileCount}，已补充一张额外牌面 {extraType} 作为临时兜底。");
        }

        Shuffle(rng, deck);
        return deck;
    }

    /// <summary>
    /// 为“已知可解的移除步骤”生成一串成对牌面。
    /// 这里每一步只需要一个牌面类型，调用方会把同一种类型赋给这一对被选中的牌。
    /// </summary>
    public static IReadOnlyList<string> BuildPairTypeSequence(int pairCount, RandomNumberGenerator rng)
    {
        var pairTypes = new List<string>(pairCount);
        for (var i = 0; i < pairCount; i++)
        {
            pairTypes.Add(MatchableTypes[rng.RandiRange(0, MatchableTypes.Length - 1)]);
        }

        return pairTypes;
    }

    /// <summary>原地打乱牌面序列。</summary>
    private static void Shuffle(RandomNumberGenerator rng, IList<string> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = rng.RandiRange(0, i);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }
}
