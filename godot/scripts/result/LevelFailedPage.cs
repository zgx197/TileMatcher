using Godot;
using TileMatcher.App;

namespace TileMatcher.Result;

/// <summary>
/// 关卡失败页。
/// 当当前局面已经没有任何可继续推进的配对时，流程层会切到这里。
/// </summary>
public partial class LevelFailedPage : Control
{
    /// <summary>失败页标题。</summary>
    private Label _headlineLabel = null!;

    /// <summary>耗时文本。</summary>
    private Label _timeValueLabel = null!;

    /// <summary>得分文本。</summary>
    private Label _scoreValueLabel = null!;

    /// <summary>配对次数文本。</summary>
    private Label _matchValueLabel = null!;

    /// <summary>失败原因文本。</summary>
    private Label _reasonLabel = null!;

    /// <summary>重新开始按钮。</summary>
    private Button _retryButton = null!;

    /// <summary>返回主页按钮。</summary>
    private Button _homeButton = null!;

    /// <summary>节点 Ready 前暂存的失败结果。</summary>
    private LevelFailedResult _result = new();

    [Signal]
    public delegate void RetryRequestedEventHandler(int levelNumber);

    [Signal]
    public delegate void ReturnHomeRequestedEventHandler();

    /// <summary>绑定节点引用并刷新失败页文本。</summary>
    public override void _Ready()
    {
        _headlineLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Headline");
        _timeValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Stats/TimeBox/VBox/Value");
        _scoreValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Stats/ScoreBox/VBox/Value");
        _matchValueLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Stats/MatchBox/VBox/Value");
        _reasonLabel = GetNode<Label>("Root/Center/Card/Margin/Stack/Reason");
        _retryButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/RetryButton");
        _homeButton = GetNode<Button>("Root/Center/Card/Margin/Stack/Buttons/HomeButton");

        _retryButton.Pressed += OnRetryPressed;
        _homeButton.Pressed += OnHomePressed;
        RefreshTexts();
    }

    /// <summary>写入失败页需要展示的结果数据。</summary>
    public void Configure(LevelFailedResult result)
    {
        _result = result;
        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    /// <summary>把当前失败结果同步到页面控件。</summary>
    private void RefreshTexts()
    {
        _headlineLabel.Text = $"关卡 {_result.LevelNumber} 未完成";
        _timeValueLabel.Text = _result.ElapsedText;
        _scoreValueLabel.Text = _result.Score.ToString();
        _matchValueLabel.Text = _result.MatchCount.ToString();
        _reasonLabel.Text = _result.FailureReason;
        _retryButton.Text = "重新来一局";
        _homeButton.Text = "返回主页";
    }

    /// <summary>通知外围流程重试当前关卡。</summary>
    private void OnRetryPressed()
    {
        EmitSignal(SignalName.RetryRequested, _result.RetryLevelNumber);
    }

    /// <summary>通知外围流程返回首页。</summary>
    private void OnHomePressed()
    {
        EmitSignal(SignalName.ReturnHomeRequested);
    }
}
