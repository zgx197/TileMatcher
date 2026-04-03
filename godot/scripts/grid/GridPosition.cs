namespace TileMatcher.Grid;

/// <summary>
/// 三维逻辑网格位置的轻量值类型。
/// </summary>
/// <remarks>
/// 当前项目里大多数地方仍直接使用 GX / GY / GZ，
/// 这个类型主要为后续更通用的网格 API 预留。
/// </remarks>
public readonly record struct GridPosition(int X, int Y, int Z);
