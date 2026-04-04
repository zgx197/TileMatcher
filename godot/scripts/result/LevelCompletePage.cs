using Godot;
using TileMatcher.App;

namespace TileMatcher.Result;

/// <summary>
/// 详细通关页。
/// 当前除了展示本局成绩，还会在存在每日奖励时给出明确提示，让玩家知道“继续”之后还会进入奖励页。
/// </summary>
public partial class LevelCompletePage : Control
{
    private Label _headlineLabel = null!;
    private Label _timeValueLabel = null!;
    private Label _scoreValueLabel = null!;
    private Label _matchValueLabel = null!;
    private Label _rewardHintLabel = null!;
    private Label _nextLevelLabel = null!;
    private Label _nextLevelSummaryLabel = null!;
    private Button _continueButton = null!;
    private Button _homeButton = null!;

    private LevelCompleteResult _result = new();

    [Signal]
    public delegate void ContinueRequestedEventHandler(int nextLevelNumber);

    [Signal]
    public delegate void ReturnHomeRequestedEventHandler();

    public override void _Ready()
    {
        _headlineLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Headline");
        _timeValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Stats/TimeBox/VBox/Value");
        _scoreValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Stats/ScoreBox/VBox/Value");
        _matchValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Stats/MatchBox/VBox/Value");
        _rewardHintLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/RewardHint");
        _nextLevelLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/NextCard/Stack/NextLevel");
        _nextLevelSummaryLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/NextCard/Stack/NextSummary");
        _continueButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/ContinueButton");
        _homeButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/HomeButton");

        _continueButton.Pressed += OnContinuePressed;
        _homeButton.Pressed += OnHomePressed;
        RefreshTexts();
    }

    public void Configure(LevelCompleteResult result)
    {
        _result = result;
        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    private void RefreshTexts()
    {
        _headlineLabel.Text = $"关卡 {_result.LevelNumber} 完成";
        _timeValueLabel.Text = _result.ElapsedText;
        _scoreValueLabel.Text = _result.Score.ToString();
        _matchValueLabel.Text = _result.MatchCount.ToString();

        _rewardHintLabel.Visible = _result.HasDailyReward;
        _rewardHintLabel.Text = _result.HasDailyReward
            ? $"今日金币奖励待查看：+{_result.DailyRewardLeafCount} 金币"
            : "本次继续将直接进入下一关";

        _nextLevelLabel.Text = string.IsNullOrWhiteSpace(_result.NextLevelName)
            ? $"下一关：关卡 {_result.NextLevelNumber}"
            : $"下一关：{_result.NextLevelName}";
        _nextLevelSummaryLabel.Text = string.IsNullOrWhiteSpace(_result.NextLevelSummary)
            ? "下一关信息准备中"
            : _result.NextLevelSummary;

        _continueButton.Text = _result.HasDailyReward
            ? "继续查看奖励"
            : $"继续前往 {_result.NextLevelNumber}";

        _homeButton.Text = "返回主页";
    }

    private void OnContinuePressed()
    {
        EmitSignal(SignalName.ContinueRequested, _result.NextLevelNumber);
    }

    private void OnHomePressed()
    {
        EmitSignal(SignalName.ReturnHomeRequested);
    }
}
