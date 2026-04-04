namespace TileMatcher.Pets;

/// <summary>
/// 单只宠物的静态定义数据。
/// 这些字段由宠物定义表提供，运行时不会直接修改。
/// </summary>
public sealed class PetDefinition
{
    /// <summary>宠物唯一标识。</summary>
    public string PetId { get; init; } = string.Empty;

    /// <summary>玩家可见的宠物名称。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>宠物物种标签。</summary>
    public string Species { get; init; } = string.Empty;

    /// <summary>宠物简介文案。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>领养所需金币。</summary>
    public int Cost { get; init; }

    /// <summary>用于首页色块显示的主色。</summary>
    public string ColorHex { get; init; } = "#E7C59D";

    /// <summary>用于首页乐园实体表现的外形标识。</summary>
    public string VisualShapeId { get; init; } = string.Empty;

    /// <summary>宠物在乐园中的默认活动文案。</summary>
    public string ParkActivityText { get; init; } = "熟悉新家";
}
