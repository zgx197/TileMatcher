namespace TileMatcher.Config;

/// <summary>
/// 单关卡在启动时使用哪一种布局来源。
/// 第一阶段先支持“原型关卡”和“按配置随机生成”两种模式，
/// 后续如果要接入手工关卡数据，可以继续在这里扩展枚举值。
/// </summary>
public enum LevelLayoutSourceMode
{
    /// <summary>使用内置原型牌桌，主要服务于调试与快速验证。</summary>
    Prototype = 0,

    /// <summary>根据规则档案动态生成一份随机布局。</summary>
    RandomGenerated = 1,

    /// <summary>从离线导出的 JSON 关卡数据中读取正式布局。</summary>
    OfflineJson = 2,
}
