using Godot;

namespace TileMatcher.Home;

/// <summary>
/// 外围流程主页。
/// 当前会同时展示玩家基础资料、进度摘要和即将进入的关卡信息。
/// </summary>
public partial class HomePage : Control
{
    private Label _playerNameLabel = null!;
    private Label _leafValueLabel = null!;
    private Label _progressSummaryLabel = null!;
    private Label _currentLevelLabel = null!;
    private Label _levelSummaryLabel = null!;
    private Button _startButton = null!;

    private int _levelNumber = 1;
    private string _pendingPlayerName = "青瓷旅人";
    private int _pendingLeafCount = 1;
    private string _pendingProgressSummary = "最高解锁 L1 · 已通关 0 局";
    private string _pendingLevelTitle = "关卡 1";
    private string _pendingLevelSummary = "规则摘要待加载";

    [Signal]
    public delegate void StartGameRequestedEventHandler(int levelNumber);

    public override void _Ready()
    {
        _playerNameLabel = GetNode<Label>("Root/Header/Bar/Left/ProfileRow/PlayerName");
        _leafValueLabel = GetNode<Label>("Root/Header/Bar/Center/LeafRow/LeafValue");
        _progressSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/ProgressSummary");
        _currentLevelLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/CurrentLevel");
        _levelSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/LevelSummary");
        _startButton = GetNode<Button>("Root/Bottom/BottomStack/StartButton");

        _startButton.Pressed += OnStartPressed;
        RefreshTexts();
    }

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

    private void RefreshTexts()
    {
        _playerNameLabel.Text = _pendingPlayerName;
        _leafValueLabel.Text = $"x{_pendingLeafCount}";
        _progressSummaryLabel.Text = _pendingProgressSummary;
        _currentLevelLabel.Text = _pendingLevelTitle;
        _levelSummaryLabel.Text = _pendingLevelSummary;
        _startButton.Text = $"进入关卡 {_levelNumber}";
    }

    private void OnStartPressed()
    {
        EmitSignal(SignalName.StartGameRequested, _levelNumber);
    }
}
