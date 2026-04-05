using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace TileMatcher.Data;

/// <summary>
/// 负责消费离线侧导出的正式关卡 JSON 与目录索引。
/// </summary>
public static class OfflineLevelJsonLoader
{
    /// <summary>离线 JSON 读取时使用的统一反序列化选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>直接从单关运行时 JSON 载入布局。</summary>
    public static LevelLayout Load(string resourcePath)
    {
        var runtimeLevel = ReadRuntimeLevel(resourcePath);
        return BuildLevelLayout(runtimeLevel);
    }

    /// <summary>从离线目录索引中解析目标关卡，再载入对应的单关 JSON。</summary>
    public static LevelLayout LoadFromCatalog(string catalogPath, int levelNumber)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new InvalidOperationException("Offline catalog path is empty.");
        }

        var entries = ReadCatalogEntries(catalogPath);
        var entry = entries.Find(item => item.LevelNumber == levelNumber);
        if (entry is null)
        {
            throw new InvalidOperationException($"Offline catalog does not contain level {levelNumber}: {catalogPath}");
        }

        if (string.IsNullOrWhiteSpace(entry.FileName))
        {
            throw new InvalidOperationException($"Offline catalog entry is missing FileName for level {levelNumber}: {catalogPath}");
        }

        var resolvedLevelPath = ResolveSiblingPath(catalogPath, "levels/" + entry.FileName);
        return Load(resolvedLevelPath);
    }

    /// <summary>读取并反序列化单关运行时 JSON。</summary>
    private static OfflineRuntimeLevelDto ReadRuntimeLevel(string resourcePath)
    {
        var json = ReadText(resourcePath);
        return JsonSerializer.Deserialize<OfflineRuntimeLevelDto>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize offline JSON file: {resourcePath}");
    }

    /// <summary>读取并反序列化离线目录索引文件。</summary>
    private static List<OfflineCatalogEntryDto> ReadCatalogEntries(string catalogPath)
    {
        var json = ReadText(catalogPath);
        return JsonSerializer.Deserialize<List<OfflineCatalogEntryDto>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize offline catalog file: {catalogPath}");
    }

    /// <summary>以 Godot 资源路径方式读取文本内容。</summary>
    private static string ReadText(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new InvalidOperationException("Offline resource path is empty.");
        }

        if (!Godot.FileAccess.FileExists(resourcePath))
        {
            throw new InvalidOperationException($"Offline resource file not found: {resourcePath}");
        }

        using var file = Godot.FileAccess.Open(resourcePath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
        {
            throw new InvalidOperationException($"Unable to open offline resource file: {resourcePath}");
        }

        return file.GetAsText();
    }

    /// <summary>基于目录文件路径解析其同级或子级资源路径。</summary>
    private static string ResolveSiblingPath(string originPath, string relativePath)
    {
        var normalizedOrigin = originPath.Replace("\\", "/");
        var lastSlashIndex = normalizedOrigin.LastIndexOf('/');
        if (lastSlashIndex < 0)
        {
            return relativePath;
        }

        var parent = normalizedOrigin[..lastSlashIndex];
        return $"{parent}/{relativePath}";
    }

    /// <summary>把离线 DTO 转成运行时布局数据。</summary>
    private static LevelLayout BuildLevelLayout(OfflineRuntimeLevelDto runtimeLevel)
    {
        if (runtimeLevel.Layout is null)
        {
            throw new InvalidOperationException("Offline runtime level is missing Layout payload.");
        }

        var layout = new LevelLayout
        {
            LevelId = runtimeLevel.Layout.LevelId > 0 ? runtimeLevel.Layout.LevelId : runtimeLevel.LevelNumber,
        };

        foreach (var tile in runtimeLevel.Layout.Tiles ?? [])
        {
            layout.Tiles.Add(new TileData
            {
                Id = tile.Id,
                Type = tile.Type ?? string.Empty,
                GX = tile.GX,
                GY = tile.GY,
                GZ = tile.GZ,
                Shape = new TileShape
                {
                    WidthUnits = tile.Shape?.WidthUnits ?? TileShape.StandardTile.WidthUnits,
                    HeightUnits = tile.Shape?.HeightUnits ?? TileShape.StandardTile.HeightUnits,
                },
                FaceHiddenInitial = tile.FaceHiddenInitial,
                IsFaceUp = !tile.FaceHiddenInitial,
                Removed = false,
                Movable = false,
            });
        }

        return layout;
    }

    /// <summary>离线目录文件中的单关索引项。</summary>
    private sealed class OfflineCatalogEntryDto
    {
        /// <summary>索引项对应的关卡号。</summary>
        public int LevelNumber { get; set; }

        /// <summary>关卡 JSON 文件名，不含目录前缀。</summary>
        public string FileName { get; set; } = string.Empty;
    }

    /// <summary>单关运行时 JSON 的根对象。</summary>
    private sealed class OfflineRuntimeLevelDto
    {
        /// <summary>当前 JSON 对应的关卡号。</summary>
        public int LevelNumber { get; set; }

        /// <summary>运行时布局主体。</summary>
        public OfflineLevelLayoutDto? Layout { get; set; }
    }

    /// <summary>离线运行时布局主体 DTO。</summary>
    private sealed class OfflineLevelLayoutDto
    {
        /// <summary>布局内部使用的关卡编号。</summary>
        public int LevelId { get; set; }

        /// <summary>当前布局包含的全部牌数据。</summary>
        public List<OfflineTileDataDto> Tiles { get; set; } = [];
    }

    /// <summary>离线单张牌 DTO。</summary>
    private sealed class OfflineTileDataDto
    {
        /// <summary>牌在布局中的唯一 id。</summary>
        public int Id { get; set; }

        /// <summary>牌面类型编码。</summary>
        public string? Type { get; set; }

        /// <summary>逻辑网格 X 坐标。</summary>
        public int GX { get; set; }

        /// <summary>逻辑网格 Y 坐标。</summary>
        public int GY { get; set; }

        /// <summary>逻辑层级坐标。</summary>
        public int GZ { get; set; }

        /// <summary>牌形尺寸定义。</summary>
        public OfflineTileShapeDto? Shape { get; set; }

        /// <summary>离线导出时记录的初始背面朝下状态。</summary>
        [JsonPropertyName("face_hidden_initial")]
        public bool FaceHiddenInitial { get; set; }
    }

    /// <summary>离线牌形尺寸 DTO。</summary>
    private sealed class OfflineTileShapeDto
    {
        /// <summary>牌形宽度，单位为逻辑微单元。</summary>
        public int WidthUnits { get; set; }

        /// <summary>牌形高度，单位为逻辑微单元。</summary>
        public int HeightUnits { get; set; }
    }
}
