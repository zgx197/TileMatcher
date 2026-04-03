using Godot;

namespace TileMatcher.Boot;

/// <summary>
/// 启动加载页。
/// 当前不接入真实资源异步加载器，而是承担正式启动氛围、最短展示时长和页面过渡职责。
/// </summary>
public partial class BootLoadingPage : Control
{
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;

    private string _pendingTitle = "青瓷旅人";
    private string _pendingSubtitle = "已解锁 1 关";
    private string _pendingStatus = "正在整理今日牌桌...";

    [Signal]
    public delegate void LoadCompletedEventHandler();

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Root/Center/Panel/Margin/Stack/Title");
        _subtitleLabel = GetNode<Label>("Root/Center/Panel/Margin/Stack/SubTitle");
        _statusLabel = GetNode<Label>("Root/Center/Panel/Margin/Stack/Status");
        _progressBar = GetNode<ProgressBar>("Root/Center/Panel/Margin/Stack/ProgressBar");

        RefreshTexts();
        PlayBootSequence();
    }

    public void Configure(string title, string subtitle, string status)
    {
        _pendingTitle = title;
        _pendingSubtitle = subtitle;
        _pendingStatus = status;

        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    private void RefreshTexts()
    {
        _titleLabel.Text = _pendingTitle;
        _subtitleLabel.Text = _pendingSubtitle;
        _statusLabel.Text = _pendingStatus;
    }

    private async void PlayBootSequence()
    {
        _progressBar.Value = 0.0;

        var tween = CreateTween();
        tween.TweenProperty(_progressBar, "value", 100.0, 1.25);
        await ToSignal(tween, Tween.SignalName.Finished);
        await ToSignal(GetTree().CreateTimer(0.18), SceneTreeTimer.SignalName.Timeout);

        EmitSignal(SignalName.LoadCompleted);
    }
}
