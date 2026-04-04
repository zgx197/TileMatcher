namespace TileMatcher.Pets;

/// <summary>
/// 玩家已经领养的宠物存档数据。
/// 当前阶段只保存最小必要信息，后续可继续扩展生活状态与互动数据。
/// </summary>
public sealed class OwnedPetData
{
    /// <summary>对应的宠物定义标识。</summary>
    public string PetId { get; set; } = string.Empty;

    /// <summary>领养时生成并保存的宠物昵称。</summary>
    public string PetName { get; set; } = string.Empty;

    /// <summary>领养时间的 UTC 时间戳文本。</summary>
    public string AdoptedAtUtc { get; set; } = string.Empty;

    /// <summary>最近一次写回存档的乐园状态文案。</summary>
    public string CurrentParkState { get; set; } = string.Empty;
}
