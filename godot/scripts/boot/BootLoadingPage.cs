using Godot;

namespace TileMatcher.Boot;

/// <summary>
/// 启动加载页。
/// 当前只负责承接正式启动氛围和最短展示时长。
/// </summary>
public partial class BootLoadingPage : Control
{
    /// <summary>启动页标题文本。</summary>
    private Label _titleLabel = null!;

    /// <summary>启动页副标题文本。</summary>
    private Label _subtitleLabel = null!;

    /// <summary>当前加载状态文本。</summary>
    private Label _statusLabel = null!;

    /// <summary>启动页进度条。</summary>
    private ProgressBar _progressBar = null!;

    /// <summary>节点 Ready 前暂存的标题。</summary>
    private string _pendingTitle = "毛球碰碰乐";

    /// <summary>节点 Ready 前暂存的副标题。</summary>
    private string _pendingSubtitle = "准备和毛茸茸伙伴一起开玩";

    /// <summary>节点 Ready 前暂存的状态说明。</summary>
    private string _pendingStatus = "正在整理今天的小动物牌桌...";

    [Signal]
    public delegate void LoadCompletedEventHandler();

    /// <summary>绑定节点引用并开始播放启动序列。</summary>
    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Root/Center/Panel/Margin/Stack/Title");
        _subtitleLabel = GetNode<Label>("Root/Center/Panel/Margin/Stack/SubTitle");
        _statusLabel = GetNode<Label>("Root/Center/Panel/Margin/Stack/Status");
        _progressBar = GetNode<ProgressBar>("Root/Center/Panel/Margin/Stack/ProgressBar");

        RefreshTexts();
        PlayBootSequence();
    }

    /// <summary>写入启动页展示文案。</summary>
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

    /// <summary>把暂存文案同步到控件上。</summary>
    private void RefreshTexts()
    {
        _titleLabel.Text = _pendingTitle;
        _subtitleLabel.Text = _pendingSubtitle;
        _statusLabel.Text = _pendingStatus;
    }

    /// <summary>播放最短启动展示动画，结束后通知外围流程继续。</summary>
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
