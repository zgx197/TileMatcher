using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace TileMatcher.App;

/// <summary>
/// 玩家外围进度存储入口。
/// 当前使用 `user://` 下的 JSON 文件，优先保证可读、可调试、可快速迭代。
/// </summary>
public static class PlayerProgressStore
{
    /// <summary>玩家进度 JSON 的固定存档路径。</summary>
    private const string SavePath = "user://player_progress.json";

    /// <summary>
    /// 读取玩家进度。
    /// 若文件不存在或解析失败，则自动创建一份默认进度。
    /// </summary>
    public static PlayerProgressData LoadOrCreate()
    {
        try
        {
            var absolutePath = ProjectSettings.GlobalizePath(SavePath);
            if (!File.Exists(absolutePath))
            {
                var fresh = new PlayerProgressData();
                Save(fresh);
                return fresh;
            }

            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<PlayerProgressData>(json);
            if (data is null)
            {
                var fallback = new PlayerProgressData();
                Save(fallback);
                return fallback;
            }

            data.CoinCount = ResolveCoinCount(json, data.CoinCount);
            data.CurrentLevelNumber = Math.Max(1, data.CurrentLevelNumber);
            data.HighestUnlockedLevel = Math.Max(data.CurrentLevelNumber, data.HighestUnlockedLevel);
            data.OwnedPets ??= [];
            data.RescueCenterPetIds ??= [];
            data.LevelAssistUsageByLevel ??= [];
            return data;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[PlayerProgressStore] 读取进度失败，已回退默认数据: {exception.Message}");
            return new PlayerProgressData();
        }
    }

    /// <summary>将当前玩家进度写回存档。</summary>
    public static void Save(PlayerProgressData data)
    {
        try
        {
            var absolutePath = ProjectSettings.GlobalizePath(SavePath);
            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(absolutePath, json);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[PlayerProgressStore] 保存进度失败: {exception.Message}");
        }
    }

    /// <summary>兼容旧字段 `LeafCount` 到当前 `CoinCount` 的迁移读取。</summary>
    private static int ResolveCoinCount(string json, int currentCoinCount)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("LeafCount", out var legacyLeafCount))
            {
                return currentCoinCount;
            }

            return legacyLeafCount.ValueKind == JsonValueKind.Number
                ? legacyLeafCount.GetInt32()
                : currentCoinCount;
        }
        catch
        {
            return currentCoinCount;
        }
    }
}
