using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace TileMatcher.Data;

/// <summary>
/// 负责消费离线侧导出的正式关卡 JSON 与目录索引。
/// </summary>
public static class OfflineLevelJsonLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static LevelLayout Load(string resourcePath)
    {
        var runtimeLevel = ReadRuntimeLevel(resourcePath);
        return BuildLevelLayout(runtimeLevel);
    }

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

    private static OfflineRuntimeLevelDto ReadRuntimeLevel(string resourcePath)
    {
        var json = ReadText(resourcePath);
        return JsonSerializer.Deserialize<OfflineRuntimeLevelDto>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize offline JSON file: {resourcePath}");
    }

    private static List<OfflineCatalogEntryDto> ReadCatalogEntries(string catalogPath)
    {
        var json = ReadText(catalogPath);
        return JsonSerializer.Deserialize<List<OfflineCatalogEntryDto>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize offline catalog file: {catalogPath}");
    }

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
                    WidthUnits = tile.Shape?.WidthUnits ?? TileShape.StandardMahjong.WidthUnits,
                    HeightUnits = tile.Shape?.HeightUnits ?? TileShape.StandardMahjong.HeightUnits,
                },
                Removed = false,
                Movable = false,
            });
        }

        return layout;
    }

    private sealed class OfflineCatalogEntryDto
    {
        public int LevelNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    private sealed class OfflineRuntimeLevelDto
    {
        public int LevelNumber { get; set; }
        public OfflineLevelLayoutDto? Layout { get; set; }
    }

    private sealed class OfflineLevelLayoutDto
    {
        public int LevelId { get; set; }
        public List<OfflineTileDataDto> Tiles { get; set; } = [];
    }

    private sealed class OfflineTileDataDto
    {
        public int Id { get; set; }
        public string? Type { get; set; }
        public int GX { get; set; }
        public int GY { get; set; }
        public int GZ { get; set; }
        public OfflineTileShapeDto? Shape { get; set; }
    }

    private sealed class OfflineTileShapeDto
    {
        public int WidthUnits { get; set; }
        public int HeightUnits { get; set; }
    }
}
