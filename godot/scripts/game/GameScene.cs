using Godot;
using TileMatcher.Board;
using TileMatcher.Config;
using TileMatcher.Layout;

namespace TileMatcher.Game;

/// <summary>
/// 当前游戏页主场景控制器。
/// </summary>
/// <remarks>
/// 它负责绑定调试 UI，并把用户操作转成对 BoardController 的调用。
/// </remarks>
public partial class GameScene : Node2D
{
    // DEBUG 悬浮按钮和屏幕边缘的最小距离。
    private const float FloatingButtonMargin = 22.0f;
    // 调试面板和屏幕边缘的最小距离。
    private const float FloatingPanelMargin = 18.0f;

    // 游戏页里真正负责棋盘逻辑和布局刷新的控制器。
    private BoardController _boardController = null!;
    // 牌桌背景，用于做闪烁反馈。
    private CanvasItem _boardBackground = null!;
    // 顶部状态栏文本。
    private Label _levelValue = null!;
    private Label _scoreValue = null!;
    private Label _matchValue = null!;
    // 调试浮窗及其子控件。
    private Control _debugOverlay = null!;
    private Control _debugPanel = null!;
    private Control _debugHeader = null!;
    private Label _debugLabel = null!;
    private Label _rulesSummaryLabel = null!;
    private Button _generateButton = null!;
    private Button _prototypeButton = null!;
    private HSlider _layerFilterSlider = null!;
    private Label _layerFilterValue = null!;
    private Button _debugToggleButton = null!;
    private Button _debugCloseButton = null!;
    private OptionButton _profileSelector = null!;

    // 以下状态用于区分“点击”和“拖拽”，避免浮动控件误触。
    private bool _debugButtonPressed;
    private bool _debugButtonDragged;
    private bool _debugPanelPressed;
    private bool _debugPanelDragged;
    private Vector2 _debugButtonPressPosition;
    private Vector2 _debugButtonStartPosition;
    private Vector2 _debugPanelPressPosition;
    private Vector2 _debugPanelStartPosition;

    public override void _Ready()
    {
        // 游戏页背景色在这里统一设置，避免依赖项目全局环境色。
        RenderingServer.SetDefaultClearColor(new Color(0.07f, 0.42f, 0.29f, 1.0f));

        _boardController = GetNode<BoardController>("BoardController");
        _boardBackground = GetNode<CanvasItem>("UI/Root/BoardBackground");
        _levelValue = GetNode<Label>("UI/Root/TopBar/Stats/LevelBox/VBox/Value");
        _scoreValue = GetNode<Label>("UI/Root/TopBar/Stats/ScoreBox/VBox/Value");
        _matchValue = GetNode<Label>("UI/Root/TopBar/Stats/MatchBox/VBox/Value");
        _debugOverlay = GetNode<Control>("UI/Root/DebugOverlay");
        _debugPanel = GetNode<Control>("UI/Root/DebugOverlay/Panel");
        _debugHeader = GetNode<Control>("UI/Root/DebugOverlay/Panel/Margin/Stack/Header");
        _debugLabel = GetNode<Label>("UI/Root/DebugOverlay/Panel/Margin/Stack/DebugLabel");
        _rulesSummaryLabel = GetNode<Label>("UI/Root/DebugOverlay/Panel/Margin/Stack/RulesSummary");
        _generateButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/Buttons/ShuffleButton");
        _prototypeButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/Buttons/PrototypeButton");
        _layerFilterSlider = GetNode<HSlider>("UI/Root/DebugOverlay/Panel/Margin/Stack/LayerInspector/Controls/Slider");
        _layerFilterValue = GetNode<Label>("UI/Root/DebugOverlay/Panel/Margin/Stack/LayerInspector/Controls/Value");
        _debugToggleButton = GetNode<Button>("UI/Root/DebugToggleButton");
        _debugCloseButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/Header/CloseButton");
        _profileSelector = GetNode<OptionButton>("UI/Root/DebugOverlay/Panel/Margin/Stack/ProfileRow/ProfileSelector");

        _levelValue.Text = "1";
        _scoreValue.Text = "0";
        _matchValue.Text = "0";
        _generateButton.Text = "随机生成";
        _prototypeButton.Text = "固定原型";
        _debugLabel.Text = "点击调试按钮后，可以查看布局摘要、规则档案和层级过滤。";
        _rulesSummaryLabel.Text = string.Empty;
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = 0;
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = false;
        _layerFilterValue.Text = "<= L0";
        _debugOverlay.Visible = false;

        _generateButton.Pressed += OnGeneratePressed;
        _prototypeButton.Pressed += OnPrototypePressed;
        _layerFilterSlider.ValueChanged += OnLayerFilterChanged;
        _debugToggleButton.GuiInput += OnDebugToggleGuiInput;
        _debugHeader.GuiInput += OnDebugPanelGuiInput;
        _debugCloseButton.Pressed += OnDebugClosePressed;
        _profileSelector.ItemSelected += OnProfileSelected;
        _boardController.BoardGenerated += OnBoardGenerated;
        GetViewport().SizeChanged += OnViewportSizeChanged;

        InitializeProfileSelector();
        InitializeDebugButtonPosition();
        _boardController.LoadPrototype();
    }

    /// <summary>响应“随机生成”按钮。</summary>
    private void OnGeneratePressed()
    {
        GD.Print("[GameScene] 点击了随机生成按钮");
        _debugLabel.Text = "正在生成新的随机堆叠结构...";
        HighlightDebugLabel(new Color(0.98f, 0.92f, 0.55f, 1.0f));
        PlayButtonFeedback(_generateButton, new Color(0.95f, 0.78f, 0.32f, 1.0f));
        FlashBoard(new Color(0.20f, 0.54f, 0.40f, 1.0f));
        _boardController.GenerateRandomBoard();
    }

    /// <summary>响应“固定原型”按钮。</summary>
    private void OnPrototypePressed()
    {
        GD.Print("[GameScene] 点击了固定原型按钮");
        _debugLabel.Text = "正在恢复固定原型布局...";
        HighlightDebugLabel(new Color(0.60f, 0.92f, 0.82f, 1.0f));
        PlayButtonFeedback(_prototypeButton, new Color(0.42f, 0.84f, 0.67f, 1.0f));
        FlashBoard(new Color(0.14f, 0.46f, 0.34f, 1.0f));
        _boardController.LoadPrototype();
    }

    /// <summary>棋盘生成完成后，同步调试文本和层过滤控件。</summary>
    private void OnBoardGenerated(string summary)
    {
        GD.Print($"[GameScene] 布局生成完成: {summary}");
        SyncLayerInspector();
        _debugLabel.Text = _boardController.GetCurrentSummary();
        _rulesSummaryLabel.Text = _boardController.GetCurrentRulesSummary();
        HighlightDebugLabel(new Color(0.92f, 0.97f, 0.86f, 1.0f));
        FlashBoard(new Color(0.07f, 0.42f, 0.29f, 1.0f));
    }

    /// <summary>响应层过滤滑杆变化。</summary>
    private void OnLayerFilterChanged(double value)
    {
        var visibleLayer = Mathf.RoundToInt((float)value);
        _boardController.SetVisibleMaxLayer(visibleLayer);
        RefreshLayerFilterText();
        _debugLabel.Text = _boardController.GetCurrentSummary();
        HighlightDebugLabel(new Color(0.82f, 0.90f, 0.99f, 1.0f));
    }

    /// <summary>处理右上角 DEBUG 悬浮按钮的点击与拖拽。</summary>
    private void OnDebugToggleGuiInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    _debugButtonPressed = true;
                    _debugButtonDragged = false;
                    _debugButtonPressPosition = mouseButton.GlobalPosition;
                    _debugButtonStartPosition = _debugToggleButton.Position;
                }
                else
                {
                    var wasDragged = _debugButtonDragged;
                    _debugButtonPressed = false;
                    _debugButtonDragged = false;

                    // 没有发生真正拖拽时，松手才视为一次点击。
                    if (!wasDragged)
                    {
                        ToggleDebugOverlay();
                    }
                }
                break;

            case InputEventMouseMotion mouseMotion when _debugButtonPressed:
                var delta = mouseMotion.GlobalPosition - _debugButtonPressPosition;
                // 只有移动距离超过阈值，才把这次输入认定为拖拽。
                if (!_debugButtonDragged && delta.Length() > 8.0f)
                {
                    _debugButtonDragged = true;
                }

                if (_debugButtonDragged)
                {
                    _debugToggleButton.Position = _debugButtonStartPosition + delta;
                    ClampDebugToggleButton();
                }
                break;
        }
    }

    /// <summary>切换调试浮窗显隐。</summary>
    private void ToggleDebugOverlay()
    {
        _debugOverlay.Visible = !_debugOverlay.Visible;
        _debugOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    /// <summary>关闭调试浮窗。</summary>
    private void OnDebugClosePressed()
    {
        if (_debugOverlay.Visible)
        {
            ToggleDebugOverlay();
        }
    }

    /// <summary>切换规则档案并重新生成棋盘。</summary>
    private void OnProfileSelected(long index)
    {
        var profiles = _boardController.GetProfiles();
        if (index < 0 || index >= profiles.Length)
        {
            return;
        }

        var profile = profiles[index];
        _boardController.SetLayoutProfile(profile.ProfileId);
        _rulesSummaryLabel.Text = _boardController.GetCurrentRulesSummary();
        _debugLabel.Text = $"已切换规则档案：{profile.DisplayName}";
        HighlightDebugLabel(new Color(0.77f, 0.92f, 1.0f, 1.0f));
        _boardController.GenerateRandomBoard();
    }

    /// <summary>视口变化时重新钳制浮动控件。</summary>
    private void OnViewportSizeChanged()
    {
        if (IsNodeReady())
        {
            ClampDebugToggleButton();
            ClampDebugPanel();
        }
    }

    /// <summary>处理调试面板标题栏的拖拽。</summary>
    private void OnDebugPanelGuiInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                if (mouseButton.Pressed)
                {
                    _debugPanelPressed = true;
                    _debugPanelDragged = false;
                    _debugPanelPressPosition = mouseButton.GlobalPosition;
                    _debugPanelStartPosition = _debugPanel.Position;
                }
                else
                {
                    _debugPanelPressed = false;
                    _debugPanelDragged = false;
                }
                break;

            case InputEventMouseMotion mouseMotion when _debugPanelPressed:
                var delta = mouseMotion.GlobalPosition - _debugPanelPressPosition;
                // 面板拖拽阈值略小于按钮，便于开发时快速调整位置。
                if (!_debugPanelDragged && delta.Length() > 6.0f)
                {
                    _debugPanelDragged = true;
                }

                if (_debugPanelDragged)
                {
                    _debugPanel.Position = _debugPanelStartPosition + delta;
                    ClampDebugPanel();
                }
                break;
        }
    }

    /// <summary>初始化 DEBUG 按钮位置。</summary>
    private void InitializeDebugButtonPosition()
    {
        var viewportSize = GetViewportRect().Size;
        _debugToggleButton.Position = new Vector2(
            viewportSize.X - _debugToggleButton.Size.X - FloatingButtonMargin,
            FloatingButtonMargin);
        ClampDebugToggleButton();
        ClampDebugPanel();
    }

    /// <summary>限制 DEBUG 按钮始终留在屏幕内。</summary>
    private void ClampDebugToggleButton()
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(FloatingButtonMargin, viewportSize.X - _debugToggleButton.Size.X - FloatingButtonMargin);
        var maxY = Mathf.Max(FloatingButtonMargin, viewportSize.Y - _debugToggleButton.Size.Y - FloatingButtonMargin);

        _debugToggleButton.Position = new Vector2(
            Mathf.Clamp(_debugToggleButton.Position.X, FloatingButtonMargin, maxX),
            Mathf.Clamp(_debugToggleButton.Position.Y, FloatingButtonMargin, maxY));
    }

    /// <summary>限制调试面板始终留在屏幕内。</summary>
    private void ClampDebugPanel()
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(FloatingPanelMargin, viewportSize.X - _debugPanel.Size.X - FloatingPanelMargin);
        var maxY = Mathf.Max(FloatingPanelMargin, viewportSize.Y - _debugPanel.Size.Y - FloatingPanelMargin);

        _debugPanel.Position = new Vector2(
            Mathf.Clamp(_debugPanel.Position.X, FloatingPanelMargin, maxX),
            Mathf.Clamp(_debugPanel.Position.Y, FloatingPanelMargin, maxY));
    }

    /// <summary>根据当前棋盘层数刷新层过滤滑杆。</summary>
    private void SyncLayerInspector()
    {
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = Mathf.Max(0, _boardController.MaxLayer);
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = _boardController.MaxLayer > 0;
        _layerFilterSlider.SetValueNoSignal(_boardController.VisibleMaxLayer);
        RefreshLayerFilterText();
    }

    /// <summary>根据档案目录填充 profile 下拉框。</summary>
    private void InitializeProfileSelector()
    {
        _profileSelector.Clear();
        var profiles = _boardController.GetProfiles();
        for (var i = 0; i < profiles.Length; i++)
        {
            _profileSelector.AddItem(profiles[i].DisplayName, i);
            if (profiles[i].ProfileId == _boardController.CurrentProfileId)
            {
                _profileSelector.Select(i);
            }
        }

        _rulesSummaryLabel.Text = _boardController.GetCurrentRulesSummary();
    }

    /// <summary>刷新“&lt;= Lx”文字。</summary>
    private void RefreshLayerFilterText()
    {
        var visibleLayer = Mathf.RoundToInt((float)_layerFilterSlider.Value);
        _layerFilterValue.Text = $"<= L{visibleLayer}";
    }

    /// <summary>为按钮播放一次轻量缩放反馈。</summary>
    private void PlayButtonFeedback(Button button, Color accentColor)
    {
        button.PivotOffset = button.Size * 0.5f;
        button.Modulate = accentColor;
        button.Scale = new Vector2(0.94f, 0.94f);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(button, "scale", Vector2.One * 1.04f, 0.08);
        tween.TweenProperty(button, "modulate", Colors.White, 0.20);
        tween.Chain().TweenProperty(button, "scale", Vector2.One, 0.12);
    }

    /// <summary>让牌桌背景闪一下，强调一次重绘或刷新。</summary>
    private void FlashBoard(Color targetColor)
    {
        _boardBackground.Modulate = new Color(1.16f, 1.16f, 1.16f, 1.0f);

        var tween = CreateTween();
        tween.TweenProperty(_boardBackground, "modulate", targetColor, 0.28);
    }

    /// <summary>让调试标签高亮后恢复。</summary>
    private void HighlightDebugLabel(Color color)
    {
        _debugLabel.Modulate = color;

        var tween = CreateTween();
        tween.TweenProperty(_debugLabel, "modulate", Colors.White, 0.35);
    }
}
