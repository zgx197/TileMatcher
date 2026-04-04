using Godot;
using TileMatcher.App;

namespace TileMatcher.Result;

/// <summary>
/// 关卡失败页。
/// 当当前局面已经没有任何可继续推进的配对时，流程层会切到这里。
/// </summary>
public partial class LevelFailedPage : Control
{
    private Label _headlineLabel = null!;
    private Label _timeValueLabel = null!;
    private Label _scoreValueLabel = null!;
    private Label _matchValueLabel = null!;
    private Label _reasonLabel = null!;
    private Button _retryButton = null!;
    private Button _homeButton = null!;

    private LevelFailedResult _result = new();

    [Signal]
    public delegate void RetryRequestedEventHandler(int levelNumber);

    [Signal]
    public delegate void ReturnHomeRequestedEventHandler();

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

    public void Configure(LevelFailedResult result)
    {
        _result = result;
        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

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

    private void OnRetryPressed()
    {
        EmitSignal(SignalName.RetryRequested, _result.RetryLevelNumber);
    }

    private void OnHomePressed()
    {
        EmitSignal(SignalName.ReturnHomeRequested);
    }
}
