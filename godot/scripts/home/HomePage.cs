using Godot;
using TileMatcher.DebugUI;

namespace TileMatcher.Home;

/// <summary>
/// 外围流程主页。
/// 当前负责展示品牌信息、关卡进度摘要，以及进入当前关卡或调试入口。
/// </summary>
public partial class HomePage : Control
{
    /// <summary>顶部品牌名文本。</summary>
    private Label _playerNameLabel = null!;

    /// <summary>顶部副标题文本。</summary>
    private Label _profileSubtitleLabel = null!;

    /// <summary>顶部资源数量文本。</summary>
    private Label _leafValueLabel = null!;

    /// <summary>首页下方的整体进度摘要。</summary>
    private Label _progressSummaryLabel = null!;

    /// <summary>当前准备进入的关卡标题。</summary>
    private Label _currentLevelLabel = null!;

    /// <summary>当前关卡规则与来源摘要。</summary>
    private Label _levelSummaryLabel = null!;

    /// <summary>首页中心主 Logo 文本。</summary>
    private Label _logoLabel = null!;

    /// <summary>首页中心副文案。</summary>
    private Label _logoCaptionLabel = null!;

    /// <summary>正式进入关卡的主按钮。</summary>
    private Button _startButton = null!;

    /// <summary>开发期快捷调试入口按钮。</summary>
    private Button _debugButton = null!;

    /// <summary>首页复用的共享调试面板。</summary>
    private SharedDebugPanel _debugPanel = null!;

    /// <summary>调试面板中的跳关输入框。</summary>
    private SpinBox _jumpLevelInput = null!;

    /// <summary>调试面板中的跳关按钮。</summary>
    private Button _jumpLevelButton = null!;

    /// <summary>调试面板中的重置当前关卡辅助次数按钮。</summary>
    private Button _resetCurrentLevelAssistButton = null!;

    /// <summary>调试面板中的重置账号数据按钮。</summary>
    private Button _resetProgressButton = null!;

    /// <summary>调试面板关闭按钮。</summary>
    private Button _debugCloseButton = null!;

    /// <summary>当前主页准备进入的关卡号。</summary>
    private int _levelNumber = 1;

    /// <summary>节点 Ready 前暂存的品牌名。</summary>
    private string _pendingPlayerName = "毛球碰碰乐";

    /// <summary>节点 Ready 前暂存的叶子数量。</summary>
    private int _pendingLeafCount = 1;

    /// <summary>节点 Ready 前暂存的进度摘要文本。</summary>
    private string _pendingProgressSummary = "最高解锁 L1 / 已通关 0 局";

    /// <summary>节点 Ready 前暂存的关卡标题。</summary>
    private string _pendingLevelTitle = "关卡 1";

    /// <summary>节点 Ready 前暂存的关卡详情摘要。</summary>
    private string _pendingLevelSummary = "关卡信息准备中";

    /// <summary>请求按当前关卡号进入正式游戏页。</summary>
    [Signal]
    public delegate void StartGameRequestedEventHandler(int levelNumber);

    /// <summary>请求按指定关卡号直接进入游戏页。</summary>
    [Signal]
    public delegate void DebugLevelJumpRequestedEventHandler(int levelNumber);

    /// <summary>请求重置当前关卡的辅助次数。</summary>
    [Signal]
    public delegate void ResetCurrentLevelAssistRequestedEventHandler(int levelNumber);

    /// <summary>请求清空全部账号进度。</summary>
    [Signal]
    public delegate void ResetProgressRequestedEventHandler();

    /// <summary>绑定节点引用并接通主页按钮事件。</summary>
    public override void _Ready()
    {
        _playerNameLabel = GetNode<Label>("Root/Header/Bar/Left/ProfileRow/PlayerName");
        _profileSubtitleLabel = GetNode<Label>("Root/Header/Bar/Left/ProfileRow/SubTitle");
        _leafValueLabel = GetNode<Label>("Root/Header/Bar/Center/LeafRow/LeafValue");
        _progressSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/ProgressSummary");
        _currentLevelLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/CurrentLevel");
        _levelSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/LevelSummary");
        _logoLabel = GetNode<Label>("Root/LogoBlock/LogoStack/Logo");
        _logoCaptionLabel = GetNode<Label>("Root/LogoBlock/LogoStack/Caption");
        _startButton = GetNode<Button>("Root/Bottom/BottomStack/StartButton");
        _debugButton = GetNode<Button>("Root/Header/Bar/Right/DebugButton");
        _debugPanel = GetNode<SharedDebugPanel>("Root/DebugPanel");
        _jumpLevelInput = _debugPanel.JumpLevelInput;
        _jumpLevelButton = _debugPanel.JumpButton;
        _resetCurrentLevelAssistButton = _debugPanel.ResetCurrentLevelAssistButton;
        _resetProgressButton = _debugPanel.ResetProgressButton;
        _debugCloseButton = _debugPanel.CloseButton;

        _startButton.Pressed += OnStartPressed;
        _debugButton.Pressed += OnDebugPressed;
        _jumpLevelButton.Pressed += OnJumpLevelPressed;
        _resetCurrentLevelAssistButton.Pressed += OnResetCurrentLevelAssistPressed;
        _resetProgressButton.Pressed += OnResetProgressPressed;
        _debugCloseButton.Pressed += OnDebugClosePressed;

        _debugPanel.TitleLabel.Text = "调试面板";
        _debugPanel.HintLabel.Text = "首页 DEBUG 只打开调试面板，不会自动进入关卡。";
        _debugPanel.ProfileRow.Visible = false;
        _debugPanel.RulesSummaryLabel.Visible = false;
        _debugPanel.DebugLabel.Visible = false;
        _debugPanel.LayerInspector.Visible = false;
        _debugPanel.GenerationButtonsRow.Visible = false;
        _debugPanel.AutoMatchButton.Visible = false;
        _debugPanel.ClosePanel();

        RefreshTexts();
    }

    /// <summary>由外围流程写入主页当前应显示的品牌与关卡信息。</summary>
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

    /// <summary>把暂存数据真正刷新到主页 UI 文本中。</summary>
    private void RefreshTexts()
    {
        _playerNameLabel.Text = _pendingPlayerName;
        _profileSubtitleLabel.Text = "欢迎回来";
        _leafValueLabel.Text = $"x{_pendingLeafCount}";
        _progressSummaryLabel.Text = _pendingProgressSummary;
        _currentLevelLabel.Text = _pendingLevelTitle;
        _levelSummaryLabel.Text = _pendingLevelSummary;
        _logoLabel.Text = "毛球\n碰碰乐";
        _logoCaptionLabel.Text = "带毛茸茸伙伴回家的轻松配对冒险";
        _startButton.Text = $"进入关卡 {_levelNumber}";
        _jumpLevelInput.Value = _levelNumber;
    }

    /// <summary>响应“进入关卡”按钮，进入当前关卡。</summary>
    private void OnStartPressed()
    {
        EmitSignal(SignalName.StartGameRequested, _levelNumber);
    }

    /// <summary>响应首页 DEBUG 按钮，只打开首页调试面板。</summary>
    private void OnDebugPressed()
    {
        _debugPanel.OpenPanel();
        _debugPanel.SetJumpLevel(_levelNumber);
    }

    /// <summary>响应调试面板中的跳关按钮。</summary>
    private void OnJumpLevelPressed()
    {
        var targetLevel = Mathf.Max(1, Mathf.RoundToInt((float)_jumpLevelInput.Value));
        _jumpLevelInput.Value = targetLevel;
        EmitSignal(SignalName.DebugLevelJumpRequested, targetLevel);
    }

    /// <summary>响应调试面板中的重置当前关卡辅助次数按钮。</summary>
    private void OnResetCurrentLevelAssistPressed()
    {
        var targetLevel = Mathf.Max(1, Mathf.RoundToInt((float)_jumpLevelInput.Value));
        _jumpLevelInput.Value = targetLevel;
        EmitSignal(SignalName.ResetCurrentLevelAssistRequested, targetLevel);
    }

    /// <summary>响应调试面板中的重置账号数据按钮。</summary>
    private void OnResetProgressPressed()
    {
        EmitSignal(SignalName.ResetProgressRequested);
    }

    /// <summary>关闭首页调试面板。</summary>
    private void OnDebugClosePressed()
    {
        _debugPanel.ClosePanel();
    }
}
