using Godot;
using TileMatcher.Logging;

namespace TileMatcher.Game;

public partial class GameScene
{
    private bool _runtimeLogUiInitialized;
    private bool _showErrorLogPreview;
    private string _runtimeLogStatusMessage = string.Empty;

    public override void _EnterTree()
    {
        base._EnterTree();
        CallDeferred(nameof(InitializeRuntimeLogUi));
    }

    private void InitializeRuntimeLogUi()
    {
        if (_runtimeLogUiInitialized || !IsNodeReady() || _debugOverlay is null)
        {
            return;
        }

        _runtimeLogUiInitialized = true;
        _debugOverlay.RefreshLogButton.Pressed += OnRefreshLogPressed;
        _debugOverlay.ShowLatestLogButton.Pressed += OnShowLatestLogPressed;
        _debugOverlay.ShowErrorLogButton.Pressed += OnShowErrorLogPressed;
        _debugOverlay.CopyLogButton.Pressed += OnCopyLogPressed;
        _debugOverlay.ClearLogsButton.Pressed += OnClearLogsPressed;
        _debugOverlay.OpenLogDirectoryButton.Pressed += OnOpenLogDirectoryPressed;

        _restartButton.Pressed += () => RuntimeLog.Info("GameScene", $"请求重开当前关卡: level={_currentLevelNumber}");
        _hintButton.Pressed += () => RuntimeLog.Info("GameScene", $"请求提示当前关卡: level={_currentLevelNumber}");
        _generateButton.Pressed += () => RuntimeLog.Info("GameScene", $"点击随机生成按钮: level={_currentLevelNumber}");
        _prototypeButton.Pressed += () => RuntimeLog.Info("GameScene", $"点击原型关卡按钮: level={_currentLevelNumber}");
        _jumpLevelButton.Pressed += () => RuntimeLog.Info("GameScene", $"请求跳关: target={Mathf.Max(1, Mathf.RoundToInt((float)_jumpLevelInput.Value))}");
        _refreshRescueCenterButton.Pressed += () => RuntimeLog.Info("GameScene", "请求立即刷新救助中心。");
        _addCoinButton.Pressed += () => RuntimeLog.Info("GameScene", $"请求追加金币: amount={Mathf.Max(1, Mathf.RoundToInt((float)_addCoinInput.Value))}");
        _resetCurrentLevelAssistButton.Pressed += () => RuntimeLog.Info("GameScene", $"请求重置当前关卡辅助次数: level={_currentLevelNumber}");
        _autoMatchButton.Pressed += () => RuntimeLog.Info("GameScene", $"请求自动消除一对: level={_currentLevelNumber}");
        _resetProgressButton.Pressed += () => RuntimeLog.Info("GameScene", "请求重置账号数据。");
        _backHomeButton.Pressed += () => RuntimeLog.Info("GameScene", $"点击返回主页按钮: level={_currentLevelNumber}");
        _settingsButton.Pressed += () => RuntimeLog.Info("GameScene", "点击顶部设置按钮，打开调试面板。");
        _boardController.BoardGenerated += OnRuntimeLogBoardGenerated;
        _boardController.BoardStateChanged += OnRuntimeLogBoardStateChanged;
        LevelCompleted += result => RuntimeLog.Info("GameScene", $"关卡完成: level={result.LevelNumber}, score={result.Score}, matches={result.MatchCount}, elapsed={result.ElapsedText}");
        LevelFailed += result => RuntimeLog.Warn("GameScene", $"关卡失败: level={result.LevelNumber}, score={result.Score}, matches={result.MatchCount}, elapsed={result.ElapsedText}");

        RuntimeLog.Info("GameScene", $"日志面板已初始化: level={_currentLevelNumber}, summary={_boardController.GetCurrentSummary()}");
        RefreshRuntimeLogPanel(false, "日志面板已初始化。");
    }

    private void OnRuntimeLogBoardGenerated(string summary)
    {
        RuntimeLog.Info("GameScene", $"布局生成完成: {summary}");
        RefreshRuntimeLogPanel(_showErrorLogPreview);
    }

    private void OnRuntimeLogBoardStateChanged(string message)
    {
        RuntimeLog.Info("GameScene", $"牌桌状态更新: {message}");
        RefreshRuntimeLogPanel(_showErrorLogPreview);
    }

    private void OnRefreshLogPressed()
    {
        RefreshRuntimeLogPanel(_showErrorLogPreview, "已刷新日志预览。");
    }

    private void OnShowLatestLogPressed()
    {
        _showErrorLogPreview = false;
        RefreshRuntimeLogPanel(false, "当前显示 latest.log。");
    }

    private void OnShowErrorLogPressed()
    {
        _showErrorLogPreview = true;
        RefreshRuntimeLogPanel(true, "当前显示 latest-error.log。");
    }

    private void OnCopyLogPressed()
    {
        var content = _debugOverlay.LogPreview.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            RefreshRuntimeLogPanel(_showErrorLogPreview, "当前没有可复制的日志内容。");
            ShowInteractionTip("当前没有可复制的日志内容。");
            return;
        }

        DisplayServer.ClipboardSet(content);
        RefreshRuntimeLogPanel(_showErrorLogPreview, "已复制当前日志预览到剪贴板。");
        ShowInteractionTip("已复制当前日志预览到剪贴板。");
    }

    private void OnClearLogsPressed()
    {
        RuntimeLog.ClearAllLogs();
        _showErrorLogPreview = false;
        RefreshRuntimeLogPanel(false, "已清空日志目录并开始新的会话日志。");
        ShowInteractionTip("已清空日志目录并开始新的会话日志。");
    }

    private void OnOpenLogDirectoryPressed()
    {
        if (RuntimeLog.TryOpenLogDirectory(out var errorMessage))
        {
            RefreshRuntimeLogPanel(_showErrorLogPreview, "已请求打开日志目录。");
            ShowInteractionTip("已请求打开日志目录。");
            return;
        }

        RefreshRuntimeLogPanel(_showErrorLogPreview, errorMessage);
        ShowInteractionTip(errorMessage);
    }

    private void RefreshRuntimeLogPanel(bool showErrorLog, string statusMessage = "")
    {
        if (!_runtimeLogUiInitialized || _debugOverlay is null)
        {
            return;
        }

        _showErrorLogPreview = showErrorLog;
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            _runtimeLogStatusMessage = statusMessage;
        }

        var snapshot = RuntimeLog.GetSnapshot();
        var previewPath = showErrorLog ? snapshot.LatestErrorLogPath : snapshot.LatestLogPath;
        var previewText = showErrorLog ? snapshot.LatestErrorLogText : snapshot.LatestLogText;
        var previewName = showErrorLog ? "latest-error.log" : "latest.log";
        var platformHint = snapshot.CanOpenDirectory
            ? "可直接打开日志目录。"
            : "当前平台不支持直接打开目录，请通过 user://logs 或 logcat 查看。";

        _debugOverlay.LogPathLabel.Text = $"日志目录: {snapshot.LogDirectoryPath}";
        _debugOverlay.LogSummaryLabel.Text =
            $"预览文件: {previewName}\n文件路径: {previewPath}\n{platformHint}\n状态: {(_runtimeLogStatusMessage.Length > 0 ? _runtimeLogStatusMessage : "就绪")}";
        _debugOverlay.LogPreview.Text = previewText;
        _debugOverlay.OpenLogDirectoryButton.Disabled = !snapshot.CanOpenDirectory;
    }
}
