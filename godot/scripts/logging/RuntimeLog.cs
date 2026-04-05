using System;
using System.IO;
using System.Text;
using Godot;

namespace TileMatcher.Logging;

/// <summary>
/// 游戏运行时统一日志入口。
/// 第一阶段先提供本地文件落盘、关键日志镜像到 Godot 输出，以及调试面板读取能力。
/// </summary>
public static class RuntimeLog
{
    private const int DefaultPreviewCharCount = 12000;
    private const string LatestLogFileName = "latest.log";
    private const string LatestErrorLogFileName = "latest-error.log";

    private static readonly object SyncRoot = new();

    private static bool _initialized;
    private static string _logDirectoryPath = string.Empty;
    private static string _sessionLogPath = string.Empty;
    private static string _sessionErrorLogPath = string.Empty;
    private static string _latestLogPath = string.Empty;
    private static string _latestErrorLogPath = string.Empty;
    private static bool _canOpenDirectory;

    public static bool IsInitialized
    {
        get
        {
            lock (SyncRoot)
            {
                return _initialized;
            }
        }
    }

    public static string LogDirectoryPath
    {
        get
        {
            lock (SyncRoot)
            {
                return _logDirectoryPath;
            }
        }
    }

    public static string LatestLogPath
    {
        get
        {
            lock (SyncRoot)
            {
                return _latestLogPath;
            }
        }
    }

    public static string LatestErrorLogPath
    {
        get
        {
            lock (SyncRoot)
            {
                return _latestErrorLogPath;
            }
        }
    }

    public static bool CanOpenDirectory
    {
        get
        {
            lock (SyncRoot)
            {
                return _canOpenDirectory;
            }
        }
    }

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            _logDirectoryPath = ResolveLogDirectoryPath();
            Directory.CreateDirectory(_logDirectoryPath);
            _canOpenDirectory = !string.Equals(OS.GetName(), "Android", StringComparison.Ordinal);
            StartNewSessionFilesNoLock(DateTimeOffset.Now);
            _initialized = true;
        }

        Info("RuntimeLog", $"日志系统已初始化，目录: {LogDirectoryPath}");
    }

    public static void Shutdown()
    {
        if (!IsInitialized)
        {
            return;
        }

        Info("RuntimeLog", "日志系统准备关闭。");
        Flush();

        lock (SyncRoot)
        {
            _initialized = false;
        }
    }

    public static void Flush()
    {
        // 第一阶段使用同步追加写入，这里保留空实现以便后续扩展为缓冲刷盘。
    }

    public static void Info(string category, string message)
    {
        Write(LogLevel.Info, category, message, null);
    }

    public static void Warn(string category, string message)
    {
        Write(LogLevel.Warning, category, message, null);
    }

    public static void Error(string category, string message, Exception? exception = null)
    {
        Write(LogLevel.Error, category, message, exception);
    }

    public static void Fatal(string category, string message, Exception? exception = null)
    {
        Write(LogLevel.Fatal, category, message, exception);
    }

    public static string ReadLatestLogText(int maxChars = DefaultPreviewCharCount)
    {
        return ReadTailText(LatestLogPath, maxChars);
    }

    public static string ReadLatestErrorLogText(int maxChars = DefaultPreviewCharCount)
    {
        return ReadTailText(LatestErrorLogPath, maxChars);
    }

    public static LogSnapshot GetSnapshot(int previewChars = DefaultPreviewCharCount)
    {
        EnsureInitialized();

        return new LogSnapshot
        {
            IsInitialized = IsInitialized,
            CanOpenDirectory = CanOpenDirectory,
            LogDirectoryPath = LogDirectoryPath,
            LatestLogPath = LatestLogPath,
            LatestErrorLogPath = LatestErrorLogPath,
            LatestLogText = ReadLatestLogText(previewChars),
            LatestErrorLogText = ReadLatestErrorLogText(previewChars),
        };
    }

    public static void ClearAllLogs()
    {
        EnsureInitialized();

        lock (SyncRoot)
        {
            if (Directory.Exists(_logDirectoryPath))
            {
                foreach (var filePath in Directory.GetFiles(_logDirectoryPath, "*.log", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(filePath);
                }
            }

            StartNewSessionFilesNoLock(DateTimeOffset.Now);
        }

        Info("RuntimeLog", "已清空日志目录并重新开始新的会话日志。");
    }

    public static bool TryOpenLogDirectory(out string errorMessage)
    {
        EnsureInitialized();

        if (string.Equals(OS.GetName(), "Android", StringComparison.Ordinal))
        {
            errorMessage = "Android 平台不支持直接打开日志目录，请通过 user://logs 或 logcat 查看。";
            return false;
        }

        var path = LogDirectoryPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            errorMessage = "日志目录不存在。";
            return false;
        }

        var result = OS.ShellOpen(path);
        if (result != Godot.Error.Ok)
        {
            errorMessage = $"打开日志目录失败，错误码: {result}";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            Initialize();
        }
    }

    private static void Write(LogLevel level, string category, string message, Exception? exception)
    {
        EnsureInitialized();

        var entry = BuildEntry(level, category, message, exception);
        lock (SyncRoot)
        {
            AppendLineNoLock(_sessionLogPath, entry);
            AppendLineNoLock(_latestLogPath, entry);

            if (level is LogLevel.Error or LogLevel.Fatal)
            {
                AppendLineNoLock(_sessionErrorLogPath, entry);
                AppendLineNoLock(_latestErrorLogPath, entry);
            }
        }

        MirrorToGodot(level, entry);
    }

    private static string BuildEntry(LogLevel level, string category, string message, Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append('[');
        builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(']');
        builder.Append('[');
        builder.Append(level.ToString().ToUpperInvariant());
        builder.Append(']');
        builder.Append('[');
        builder.Append(string.IsNullOrWhiteSpace(category) ? "General" : category);
        builder.Append("] ");
        builder.Append(message ?? string.Empty);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        return builder.ToString();
    }

    private static void MirrorToGodot(LogLevel level, string entry)
    {
        switch (level)
        {
            case LogLevel.Warning:
                GD.PushWarning(entry);
                break;

            case LogLevel.Error:
            case LogLevel.Fatal:
                GD.PushError(entry);
                break;

            default:
                GD.Print(entry);
                break;
        }
    }

    private static string ResolveLogDirectoryPath()
    {
        var userLogPath = ProjectSettings.GlobalizePath("user://logs");
        if (string.Equals(OS.GetName(), "Android", StringComparison.Ordinal))
        {
            return userLogPath;
        }

        if (string.Equals(OS.GetName(), "Windows", StringComparison.Ordinal) && !OS.HasFeature("editor"))
        {
            try
            {
                var executablePath = OS.GetExecutablePath();
                var executableDirectory = Path.GetDirectoryName(executablePath);
                if (!string.IsNullOrWhiteSpace(executableDirectory))
                {
                    var exportLogPath = Path.Combine(executableDirectory, "logs");
                    Directory.CreateDirectory(exportLogPath);
                    return exportLogPath;
                }
            }
            catch
            {
                return userLogPath;
            }
        }

        return userLogPath;
    }

    private static void StartNewSessionFilesNoLock(DateTimeOffset sessionTime)
    {
        var sessionStamp = sessionTime.ToString("yyyyMMdd-HHmmss");
        _sessionLogPath = Path.Combine(_logDirectoryPath, $"session-{sessionStamp}.log");
        _sessionErrorLogPath = Path.Combine(_logDirectoryPath, $"session-{sessionStamp}-error.log");
        _latestLogPath = Path.Combine(_logDirectoryPath, LatestLogFileName);
        _latestErrorLogPath = Path.Combine(_logDirectoryPath, LatestErrorLogFileName);

        File.WriteAllText(_sessionLogPath, string.Empty, Encoding.UTF8);
        File.WriteAllText(_sessionErrorLogPath, string.Empty, Encoding.UTF8);
        File.WriteAllText(_latestLogPath, string.Empty, Encoding.UTF8);
        File.WriteAllText(_latestErrorLogPath, string.Empty, Encoding.UTF8);
    }

    private static void AppendLineNoLock(string filePath, string text)
    {
        File.AppendAllText(filePath, text + System.Environment.NewLine, Encoding.UTF8);
    }

    private static string ReadTailText(string filePath, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return "日志文件尚未生成。";
        }

        try
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(content))
            {
                return "日志文件当前为空。";
            }

            if (maxChars <= 0 || content.Length <= maxChars)
            {
                return content;
            }

            return $"... 仅显示末尾 {maxChars} 个字符 ...{System.Environment.NewLine}{content[^maxChars..]}";
        }
        catch (Exception exception)
        {
            return $"读取日志失败: {exception.Message}";
        }
    }
}
