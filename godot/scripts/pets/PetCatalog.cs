using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace TileMatcher.Pets;

/// <summary>
/// 宠物定义表目录。
/// 负责从 JSON 中读取宠物静态配置，并提供按 id 查询能力。
/// </summary>
public sealed class PetCatalog
{
    /// <summary>空目录实例，用于加载失败时回退。</summary>
    public static PetCatalog Empty { get; } = new([]);

    private readonly Dictionary<string, PetDefinition> _definitionsById;

    /// <summary>宠物定义列表，按配置文件顺序保留。</summary>
    public IReadOnlyList<PetDefinition> Definitions { get; }

    /// <summary>用定义列表构造宠物目录。</summary>
    public PetCatalog(IReadOnlyList<PetDefinition> definitions)
    {
        Definitions = definitions;
        _definitionsById = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.PetId))
            .ToDictionary(definition => definition.PetId, definition => definition, StringComparer.Ordinal);
    }

    /// <summary>从 JSON 资源路径加载宠物定义表。</summary>
    public static PetCatalog Load(string resourcePath)
    {
        try
        {
            if (!Godot.FileAccess.FileExists(resourcePath))
            {
                GD.PushWarning($"[PetCatalog] 宠物定义表不存在: {resourcePath}");
                return Empty;
            }

            var json = Godot.FileAccess.GetFileAsString(resourcePath);
            var entries = JsonSerializer.Deserialize<List<PetDefinitionDto>>(json, JsonOptions);
            if (entries is null || entries.Count == 0)
            {
                GD.PushWarning($"[PetCatalog] 宠物定义表为空: {resourcePath}");
                return Empty;
            }

            var definitions = new List<PetDefinition>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.PetId))
                {
                    continue;
                }

                definitions.Add(new PetDefinition
                {
                    PetId = entry.PetId,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.PetId : entry.DisplayName,
                    Species = string.IsNullOrWhiteSpace(entry.Species) ? "小动物" : entry.Species,
                    Description = string.IsNullOrWhiteSpace(entry.Description) ? "等待被带回家的小伙伴。" : entry.Description,
                    Cost = Math.Max(0, entry.Cost),
                    ColorHex = string.IsNullOrWhiteSpace(entry.ColorHex) ? "#E7C59D" : entry.ColorHex,
                    ParkActivityText = string.IsNullOrWhiteSpace(entry.ParkActivityText) ? "熟悉新家" : entry.ParkActivityText,
                });
            }

            return definitions.Count == 0 ? Empty : new PetCatalog(definitions);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[PetCatalog] 加载宠物定义表失败: path={resourcePath}, error={exception.Message}");
            return Empty;
        }
    }

    /// <summary>按宠物 id 查询定义。</summary>
    public bool TryGetDefinition(string petId, out PetDefinition definition)
    {
        return _definitionsById.TryGetValue(petId, out definition!);
    }

    /// <summary>按宠物 id 列表解析定义，并忽略不存在的项。</summary>
    public IReadOnlyList<PetDefinition> ResolveDefinitions(IReadOnlyList<string> petIds)
    {
        var definitions = new List<PetDefinition>(petIds.Count);
        foreach (var petId in petIds)
        {
            if (TryGetDefinition(petId, out var definition))
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>宠物定义 JSON 对应的中间 DTO。</summary>
    private sealed class PetDefinitionDto
    {
        [JsonPropertyName("pet_id")]
        public string PetId { get; init; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; init; } = string.Empty;

        [JsonPropertyName("species")]
        public string Species { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("cost")]
        public int Cost { get; init; }

        [JsonPropertyName("color_hex")]
        public string ColorHex { get; init; } = string.Empty;

        [JsonPropertyName("park_activity_text")]
        public string ParkActivityText { get; init; } = string.Empty;
    }
}
