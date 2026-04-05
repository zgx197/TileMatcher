using Godot;
using TileMatcher.App;

namespace TileMatcher.Result;

/// <summary>
/// 详细通关页。
/// 当前除了展示本局成绩，还会在存在每日奖励时给出明确提示，让玩家知道“继续”之后还会进入奖励页。
/// </summary>
public partial class LevelCompletePage : Control
{
    /// <summary>通关页标题。</summary>
    private Label _headlineLabel = null!;

    /// <summary>耗时文本。</summary>
    private Label _timeValueLabel = null!;

    /// <summary>得分文本。</summary>
    private Label _scoreValueLabel = null!;

    /// <summary>配对次数文本。</summary>
    private Label _matchValueLabel = null!;

    /// <summary>每日奖励提示文本。</summary>
    private Label _rewardHintLabel = null!;

    /// <summary>下一关标题文本。</summary>
    private Label _nextLevelLabel = null!;

    /// <summary>下一关摘要文本。</summary>
    private Label _nextLevelSummaryLabel = null!;

    /// <summary>继续按钮。</summary>
    private Button _continueButton = null!;

    /// <summary>返回主页按钮。</summary>
    private Button _homeButton = null!;

    /// <summary>节点 Ready 前暂存的通关结果。</summary>
    private LevelCompleteResult _result = new();

    [Signal]
    public delegate void ContinueRequestedEventHandler(int nextLevelNumber);

    [Signal]
    public delegate void ReturnHomeRequestedEventHandler();

    /// <summary>绑定节点引用并刷新通关页文本。</summary>
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

    /// <summary>写入通关页需要展示的结果数据。</summary>
    public void Configure(LevelCompleteResult result)
    {
        _result = result;
        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    /// <summary>把当前结果刷新到页面控件。</summary>
    private void RefreshTexts()
    {
        _headlineLabel.Text = $"关卡 {_result.LevelNumber} 完成";
        _timeValueLabel.Text = _result.ElapsedText;
        _scoreValueLabel.Text = _result.Score.ToString();
        _matchValueLabel.Text = _result.MatchCount.ToString();

        _rewardHintLabel.Visible = _result.HasDailyReward;
        _rewardHintLabel.Text = _result.HasDailyReward
            ? $"今日金币奖励待查看：+{_result.DailyRewardCoinCount} 金币"
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

    /// <summary>通知外围流程继续后续流程。</summary>
    private void OnContinuePressed()
    {
        EmitSignal(SignalName.ContinueRequested, _result.NextLevelNumber);
    }

    /// <summary>通知外围流程返回首页。</summary>
    private void OnHomePressed()
    {
        EmitSignal(SignalName.ReturnHomeRequested);
    }
}
