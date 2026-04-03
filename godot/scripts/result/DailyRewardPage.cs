using Godot;
using TileMatcher.App;

namespace TileMatcher.Result;

/// <summary>
/// 每日奖励页。
/// 作为通关后的次级奖励反馈页存在，视觉和流程上与结算页分离。
/// </summary>
public partial class DailyRewardPage : Control
{
    private Label _headlineLabel = null!;
    private Label _rewardValueLabel = null!;
    private Label _descriptionLabel = null!;
    private Label _leafTotalLabel = null!;
    private Label _nextLevelLabel = null!;
    private Label _nextSummaryLabel = null!;
    private Button _continueButton = null!;
    private Button _homeButton = null!;

    private DailyRewardSummary _summary = new();

    [Signal]
    public delegate void ContinueRequestedEventHandler(int nextLevelNumber);

    [Signal]
    public delegate void ReturnHomeRequestedEventHandler();

    public override void _Ready()
    {
        _headlineLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Headline");
        _rewardValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/RewardCard/Stack/RewardValue");
        _descriptionLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Description");
        _leafTotalLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/RewardCard/Stack/LeafTotal");
        _nextLevelLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/NextCard/Stack/NextLevel");
        _nextSummaryLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/NextCard/Stack/NextSummary");
        _continueButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/ContinueButton");
        _homeButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/HomeButton");

        _continueButton.Pressed += OnContinuePressed;
        _homeButton.Pressed += OnHomePressed;
        RefreshTexts();
    }

    public void Configure(DailyRewardSummary summary)
    {
        _summary = summary;
        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    private void RefreshTexts()
    {
        _headlineLabel.Text = _summary.RewardTitle;
        _rewardValueLabel.Text = $"+{_summary.RewardLeafCount}";
        _descriptionLabel.Text = _summary.RewardDescription;
        _leafTotalLabel.Text = $"当前叶子：x{_summary.CurrentLeafTotal}";
        _nextLevelLabel.Text = string.IsNullOrWhiteSpace(_summary.NextLevelName)
            ? $"下一关：关卡 {_summary.NextLevelNumber}"
            : $"下一关：{_summary.NextLevelName}";
        _nextSummaryLabel.Text = string.IsNullOrWhiteSpace(_summary.NextLevelSummary)
            ? "下一关规则摘要待加载"
            : _summary.NextLevelSummary;
        _continueButton.Text = $"继续前往 {_summary.NextLevelNumber}";
    }

    private void OnContinuePressed()
    {
        EmitSignal(SignalName.ContinueRequested, _summary.NextLevelNumber);
    }

    private void OnHomePressed()
    {
        EmitSignal(SignalName.ReturnHomeRequested);
    }
}
