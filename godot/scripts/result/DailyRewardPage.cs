using Godot;
using TileMatcher.App;

namespace TileMatcher.Result;

/// <summary>
/// 每日奖励页。
/// 作为通关后的次级奖励反馈页存在，视觉和流程上与结算页分离。
/// </summary>
public partial class DailyRewardPage : Control
{
    /// <summary>奖励页主标题。</summary>
    private Label _headlineLabel = null!;

    /// <summary>奖励数值文本。</summary>
    private Label _rewardValueLabel = null!;

    /// <summary>奖励说明文本。</summary>
    private Label _descriptionLabel = null!;

    /// <summary>当前金币总数文本。</summary>
    private Label _coinTotalLabel = null!;

    /// <summary>下一关标题文本。</summary>
    private Label _nextLevelLabel = null!;

    /// <summary>下一关摘要文本。</summary>
    private Label _nextSummaryLabel = null!;

    /// <summary>继续前往下一关按钮。</summary>
    private Button _continueButton = null!;

    /// <summary>返回主页按钮。</summary>
    private Button _homeButton = null!;

    /// <summary>节点 Ready 前暂存的奖励数据。</summary>
    private DailyRewardSummary _summary = new();

    [Signal]
    public delegate void ContinueRequestedEventHandler(int nextLevelNumber);

    [Signal]
    public delegate void ReturnHomeRequestedEventHandler();

    /// <summary>绑定节点引用并刷新奖励页文本。</summary>
    public override void _Ready()
    {
        _headlineLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Headline");
        _rewardValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/RewardCard/Stack/RewardValue");
        _descriptionLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Description");
        _coinTotalLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/RewardCard/Stack/CoinTotal");
        _nextLevelLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/NextCard/Stack/NextLevel");
        _nextSummaryLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/NextCard/Stack/NextSummary");
        _continueButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/ContinueButton");
        _homeButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/HomeButton");

        _continueButton.Pressed += OnContinuePressed;
        _homeButton.Pressed += OnHomePressed;
        RefreshTexts();
    }

    /// <summary>写入奖励页需要展示的数据。</summary>
    public void Configure(DailyRewardSummary summary)
    {
        _summary = summary;
        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    /// <summary>把暂存数据同步到页面控件上。</summary>
    private void RefreshTexts()
    {
        _headlineLabel.Text = _summary.RewardTitle;
        _rewardValueLabel.Text = $"+{_summary.RewardCoinCount}";
        _descriptionLabel.Text = _summary.RewardDescription;
        _coinTotalLabel.Text = $"当前金币：x{_summary.CurrentCoinTotal}";
        _nextLevelLabel.Text = string.IsNullOrWhiteSpace(_summary.NextLevelName)
            ? $"下一关：关卡 {_summary.NextLevelNumber}"
            : $"下一关：{_summary.NextLevelName}";
        _nextSummaryLabel.Text = string.IsNullOrWhiteSpace(_summary.NextLevelSummary)
            ? "下一关信息准备中"
            : _summary.NextLevelSummary;
        _continueButton.Text = $"继续前往 {_summary.NextLevelNumber}";
        _homeButton.Text = "返回主页";
    }

    /// <summary>通知外围流程继续前往下一关。</summary>
    private void OnContinuePressed()
    {
        EmitSignal(SignalName.ContinueRequested, _summary.NextLevelNumber);
    }

    /// <summary>通知外围流程返回首页。</summary>
    private void OnHomePressed()
    {
        EmitSignal(SignalName.ReturnHomeRequested);
    }
}
