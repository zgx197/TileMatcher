using System;
using System.IO;
using System.Text.Json;
using Godot;
using TileMatcher.Logging;

namespace TileMatcher.App;

/// <summary>
/// 玩家外部进度存储入口。
/// 当前使用 `user://player_progress.json` 作为本地 JSON 存档。
/// </summary>
public static class PlayerProgressStore
{
    private const string SavePath = "user://player_progress.json";

    public static PlayerProgressData LoadOrCreate()
    {
        try
        {
            var absolutePath = ProjectSettings.GlobalizePath(SavePath);
            if (!File.Exists(absolutePath))
            {
                var fresh = new PlayerProgressData();
                Save(fresh);
                RuntimeLog.Info("PlayerProgressStore", $"未找到存档，已创建默认进度: {absolutePath}");
                return fresh;
            }

            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<PlayerProgressData>(json);
            if (data is null)
            {
                var fallback = new PlayerProgressData();
                Save(fallback);
                RuntimeLog.Warn("PlayerProgressStore", $"存档反序列化结果为空，已回退默认进度: {absolutePath}");
                return fallback;
            }

            data.CoinCount = ResolveCoinCount(json, data.CoinCount);
            data.CurrentLevelNumber = Math.Max(1, data.CurrentLevelNumber);
            data.HighestUnlockedLevel = Math.Max(data.CurrentLevelNumber, data.HighestUnlockedLevel);
            data.OwnedPets ??= [];
            data.RescueCenterPetIds ??= [];
            data.LevelAssistUsageByLevel ??= [];
            RuntimeLog.Info("PlayerProgressStore", $"已加载玩家进度: {absolutePath}");
            return data;
        }
        catch (Exception exception)
        {
            RuntimeLog.Warn("PlayerProgressStore", $"读取进度失败，已回退默认数据: {exception.Message}");
            return new PlayerProgressData();
        }
    }

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
            RuntimeLog.Info("PlayerProgressStore", $"已保存玩家进度: {absolutePath}");
        }
        catch (Exception exception)
        {
            RuntimeLog.Warn("PlayerProgressStore", $"保存进度失败: {exception.Message}");
        }
    }

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
