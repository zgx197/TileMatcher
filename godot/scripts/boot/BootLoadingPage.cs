using Godot;

namespace TileMatcher.Boot;

/// <summary>
/// 启动加载页。
/// 当前只负责承接正式启动氛围和最短展示时长。
/// </summary>
public partial class BootLoadingPage : Control
{
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;

    private string _pendingTitle = "毛球碰碰乐";
    private string _pendingSubtitle = "准备和毛茸茸伙伴一起开玩";
    private string _pendingStatus = "正在整理今天的小动物牌桌...";

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
        _pendingTitle = "毛球碰碰乐";
        _pendingSubtitle = "准备和毛茸茸伙伴一起开玩";
        _pendingStatus = "正在整理今天的小动物牌桌...";

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
