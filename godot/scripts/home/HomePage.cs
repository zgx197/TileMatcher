using Godot;

namespace TileMatcher.Home;

/// <summary>
/// 外围流程的主页页面。
/// 当前版本除了提供“进入游戏”按钮，还会显示当前将进入的关卡名和规则摘要，
/// 让主页不再只是一个空壳按钮页，而是具备正式手游流程里的“出发前确认”信息。
/// </summary>
public partial class HomePage : Control
{
    private Label _playerNameLabel = null!;
    private Label _leafValueLabel = null!;
    private Label _currentLevelLabel = null!;
    private Label _levelSummaryLabel = null!;
    private Button _startButton = null!;

    private int _levelNumber = 1;
    private string _pendingPlayerName = "青雀旅人";
    private int _pendingLeafCount = 1;
    private string _pendingLevelTitle = "关卡 1";
    private string _pendingLevelSummary = "规则摘要待加载";

    [Signal]
    public delegate void StartGameRequestedEventHandler(int levelNumber);

    public override void _Ready()
    {
        _playerNameLabel = GetNode<Label>("Root/Header/Bar/Left/ProfileRow/PlayerName");
        _leafValueLabel = GetNode<Label>("Root/Header/Bar/Center/LeafRow/LeafValue");
        _currentLevelLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/CurrentLevel");
        _levelSummaryLabel = GetNode<Label>("Root/Bottom/BottomStack/InfoCard/Stack/LevelSummary");
        _startButton = GetNode<Button>("Root/Bottom/BottomStack/StartButton");

        _startButton.Pressed += OnStartPressed;
        RefreshTexts();
    }

    /// <summary>
    /// 由外围流程注入主页需要展示的最小信息。
    /// </summary>
    public void Configure(int levelNumber, string playerName, int leafCount, string levelTitle, string levelSummary)
    {
        _levelNumber = levelNumber;
        _pendingPlayerName = playerName;
        _pendingLeafCount = leafCount;
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
        _currentLevelLabel.Text = _pendingLevelTitle;
        _levelSummaryLabel.Text = _pendingLevelSummary;
        _startButton.Text = $"进入关卡 {_levelNumber}";
    }

    private void OnStartPressed()
    {
        EmitSignal(SignalName.StartGameRequested, _levelNumber);
    }
}
