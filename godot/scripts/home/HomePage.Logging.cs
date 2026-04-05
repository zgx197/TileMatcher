using Godot;
using TileMatcher.Logging;

namespace TileMatcher.Home;

public partial class HomePage
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
        if (_runtimeLogUiInitialized || !IsNodeReady() || _debugPanel is null)
        {
            return;
        }

        _runtimeLogUiInitialized = true;
        _debugPanel.RefreshLogButton.Pressed += OnRefreshLogPressed;
        _debugPanel.ShowLatestLogButton.Pressed += OnShowLatestLogPressed;
        _debugPanel.ShowErrorLogButton.Pressed += OnShowErrorLogPressed;
        _debugPanel.CopyLogButton.Pressed += OnCopyLogPressed;
        _debugPanel.ClearLogsButton.Pressed += OnClearLogsPressed;
        _debugPanel.OpenLogDirectoryButton.Pressed += OnOpenLogDirectoryPressed;
        _desktopDebugButton.Pressed += () => RefreshRuntimeLogPanel(_showErrorLogPreview, "已打开首页调试面板。");
        _mobileDebugButton.Pressed += () => RefreshRuntimeLogPanel(_showErrorLogPreview, "已打开首页调试面板。");

        RuntimeLog.Info("HomePage", "首页日志面板已初始化。");
        RefreshRuntimeLogPanel(false, "日志面板已初始化。");
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
        var content = _debugPanel.LogPreview.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            RefreshRuntimeLogPanel(_showErrorLogPreview, "当前没有可复制的日志内容。");
            return;
        }

        DisplayServer.ClipboardSet(content);
        RefreshRuntimeLogPanel(_showErrorLogPreview, "已复制当前日志预览到剪贴板。");
    }

    private void OnClearLogsPressed()
    {
        RuntimeLog.ClearAllLogs();
        _showErrorLogPreview = false;
        RefreshRuntimeLogPanel(false, "已清空日志目录并开始新的会话日志。");
    }

    private void OnOpenLogDirectoryPressed()
    {
        if (RuntimeLog.TryOpenLogDirectory(out var errorMessage))
        {
            RefreshRuntimeLogPanel(_showErrorLogPreview, "已请求打开日志目录。");
            return;
        }

        RefreshRuntimeLogPanel(_showErrorLogPreview, errorMessage);
    }

    private void RefreshRuntimeLogPanel(bool showErrorLog, string statusMessage = "")
    {
        if (!_runtimeLogUiInitialized || _debugPanel is null)
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

        _debugPanel.LogPathLabel.Text = $"日志目录: {snapshot.LogDirectoryPath}";
        _debugPanel.LogSummaryLabel.Text =
            $"预览文件: {previewName}\n文件路径: {previewPath}\n{platformHint}\n状态: {(_runtimeLogStatusMessage.Length > 0 ? _runtimeLogStatusMessage : "就绪")}";
        _debugPanel.LogPreview.Text = previewText;
        _debugPanel.OpenLogDirectoryButton.Disabled = !snapshot.CanOpenDirectory;
    }
}
