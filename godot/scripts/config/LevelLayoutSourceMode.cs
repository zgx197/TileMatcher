namespace TileMatcher.Config;

/// <summary>
/// 单关卡在启动时使用哪一种布局来源。
/// 第一阶段先支持“原型关卡”和“按配置随机生成”两种模式，
/// 后续如果要接入手工关卡数据，可以继续在这里扩展枚举值。
/// </summary>
public enum LevelLayoutSourceMode
{
    Prototype = 0,
    RandomGenerated = 1,
    OfflineJson = 2,
}
