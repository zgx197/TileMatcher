using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace TileMatcher.App;

/// <summary>
/// 玩家外围进度存储入口。
/// 当前使用 user:// 下的 JSON 文件，优先保证可读、可调试、可快速迭代。
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

            data.CurrentLevelNumber = Math.Max(1, data.CurrentLevelNumber);
            data.HighestUnlockedLevel = Math.Max(data.CurrentLevelNumber, data.HighestUnlockedLevel);
            data.LevelAssistUsageByLevel ??= [];
            return data;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[PlayerProgressStore] 读取进度失败，已回退默认数据: {exception.Message}");
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
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[PlayerProgressStore] 保存进度失败: {exception.Message}");
        }
    }
}
