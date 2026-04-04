using Godot;

namespace TileMatcher.Home;

/// <summary>
/// 外围流程主页。
/// 当前负责展示玩家信息、关卡进度摘要，以及进入当前关卡或调试入口。
/// </summary>
public partial class HomePage : Control
{
    /// <summary>顶部玩家名称文本。</summary>
    private Label _playerNameLabel = null!;

    /// <summary>顶部叶子数量文本。</summary>
    private Label _leafValueLabel = null!;

    /// <summary>主页下方的整体进度摘要。</summary>
    private Label _progressSummaryLabel = null!;

    /// <summary>当前准备进入的关卡标题。</summary>
    private Label _currentLevelLabel = null!;

    /// <summary>当前关卡的规则与来源摘要。</summary>
    private Label _levelSummaryLabel = null!;

    /// <summary>正式进入关卡的主按钮。</summary>
    private Button _startButton = null!;

    /// <summary>开发期快捷调试入口按钮。</summary>
    private Button _debugButton = null!;

    /// <summary>当前主页准备进入的关卡号。</summary>
    private int _levelNumber = 1;

    /// <summary>节点尚未 Ready 前暂存的玩家名称。</summary>
    private string _pendingPlayerName = "青瓷旅人";

    /// <summary>节点尚未 Ready 前暂存的叶子数量。</summary>
    private int _pendingLeafCount = 1;

    /// <summary>节点尚未 Ready 前暂存的进度摘要文本。</summary>
    private string _pendingProgressSummary = "最高解锁 L1 / 已通关 0 局";

    /// <summary>节点尚未 Ready 前暂存的关卡标题。</summary>
    private string _pendingLevelTitle = "关卡 1";

    /// <summary>节点尚未 Ready 前暂存的关卡详情摘要。</summary>
    private string _pendingLevelSummary = "规则摘要待加载";

    /// <summary>请求按当前关卡号进入正式游戏页。</summary>
    [Signal]
    public delegate void StartGameRequestedEventHandler(int levelNumber);

    /// <summary>请求按当前关卡号进入游戏页并直接展开调试面板。</summary>
    [Signal]
    public delegate void DebugEnterRequestedEventHandler(int levelNumber);

    /// <summary>绑定节点引用并接通主页按钮事件。</summary>
    public override void _Ready()
    {
        _playerNameLabel = GetNode<Label>("Root/Header/Bar/Left/ProfileRow/PlayerName");
        _leafValueLabel = GetNode<Label>("Root/Header/Bar/Center/LeafRow/LeafValue");
        _progressSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/ProgressSummary");
        _currentLevelLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/CurrentLevel");
        _levelSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/LevelSummary");
        _startButton = GetNode<Button>("Root/Bottom/BottomStack/StartButton");
        _debugButton = GetNode<Button>("Root/Header/Bar/Right/DebugButton");

        _startButton.Pressed += OnStartPressed;
        _debugButton.Pressed += OnDebugPressed;
        RefreshTexts();
    }

    /// <summary>由外围流程写入主页当前应显示的玩家与关卡信息。</summary>
    public void Configure(
        int levelNumber,
        string playerName,
        int leafCount,
        string progressSummary,
        string levelTitle,
        string levelSummary)
    {
        _levelNumber = levelNumber;
        _pendingPlayerName = playerName;
        _pendingLeafCount = leafCount;
        _pendingProgressSummary = progressSummary;
        _pendingLevelTitle = levelTitle;
        _pendingLevelSummary = levelSummary;

        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    /// <summary>把暂存数据真正刷新到主页 UI 文本上。</summary>
    private void RefreshTexts()
    {
        _playerNameLabel.Text = _pendingPlayerName;
        _leafValueLabel.Text = $"x{_pendingLeafCount}";
        _progressSummaryLabel.Text = _pendingProgressSummary;
        _currentLevelLabel.Text = _pendingLevelTitle;
        _levelSummaryLabel.Text = _pendingLevelSummary;
        _startButton.Text = $"进入关卡 {_levelNumber}";
    }

    /// <summary>响应“进入关卡”按钮，进入当前关卡。</summary>
    private void OnStartPressed()
    {
        EmitSignal(SignalName.StartGameRequested, _levelNumber);
    }

    /// <summary>响应首页 DEBUG 按钮，进入当前关卡并要求展开调试面板。</summary>
    private void OnDebugPressed()
    {
        EmitSignal(SignalName.DebugEnterRequested, _levelNumber);
    }
}
