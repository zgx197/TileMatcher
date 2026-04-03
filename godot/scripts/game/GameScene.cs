using Godot;
using TileMatcher.Board;

namespace TileMatcher.Game;

/// <summary>
/// 当前游戏页主场景控制器。
/// </summary>
/// <remarks>
/// 它负责绑定调试 UI，并把用户操作转成对 BoardController 的调用。
/// 这一层不直接实现牌桌规则，只负责把状态呈现出来。
/// </remarks>
public partial class GameScene : Node2D
{
    /// <summary>DEBUG 悬浮按钮距离屏幕边缘的安全间距。</summary>
    private const float FloatingButtonMargin = 22.0f;

    /// <summary>调试面板距离屏幕边缘的安全间距。</summary>
    private const float FloatingPanelMargin = 18.0f;

    /// <summary>负责牌桌运行时逻辑和交互的主控制器。</summary>
    private BoardController _boardController = null!;

    /// <summary>牌桌背景，用于做轻量闪烁反馈。</summary>
    private CanvasItem _boardBackground = null!;

    /// <summary>顶部状态栏文本。</summary>
    private Label _levelValue = null!;
    private Label _scoreValue = null!;
    private Label _matchValue = null!;

    /// <summary>调试悬浮层及其内部控件。</summary>
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

    /// <summary>以下状态用于区分“点击”和“拖动”，避免浮动控件误触。</summary>
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
        _generateButton.Text = "随机生成";
        _prototypeButton.Text = "固定原型";
        _debugLabel.Text = "点击牌桌中的可移动麻将，可以先验证基础配对消除逻辑。";
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
        _boardController.BoardStateChanged += OnBoardStateChanged;
        GetViewport().SizeChanged += OnViewportSizeChanged;

        InitializeProfileSelector();
        InitializeDebugButtonPosition();
        SyncStats();
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

    /// <summary>牌桌生成完成后，同步摘要、统计和调试控件。</summary>
    private void OnBoardGenerated(string summary)
    {
        GD.Print($"[GameScene] 布局生成完成: {summary}");
        SyncLayerInspector();
        SyncStats();
        _debugLabel.Text = _boardController.GetCurrentSummary();
        _rulesSummaryLabel.Text = _boardController.GetCurrentRulesSummary();
        HighlightDebugLabel(new Color(0.92f, 0.97f, 0.86f, 1.0f));
        FlashBoard(new Color(0.07f, 0.42f, 0.29f, 1.0f));
    }

    /// <summary>牌桌内部状态变化后，同步顶部统计和调试文本。</summary>
    private void OnBoardStateChanged(string message)
    {
        SyncStats();
        _debugLabel.Text = message;
        _rulesSummaryLabel.Text = _boardController.GetCurrentRulesSummary();
        HighlightDebugLabel(new Color(0.82f, 0.90f, 0.99f, 1.0f));
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

    /// <summary>处理 DEBUG 悬浮按钮的点击与拖动。</summary>
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

                    if (!wasDragged)
                    {
                        ToggleDebugOverlay();
                    }
                }
                break;

            case InputEventMouseMotion mouseMotion when _debugButtonPressed:
                var delta = mouseMotion.GlobalPosition - _debugButtonPressPosition;
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

    /// <summary>切换规则 profile 并重新生成牌桌。</summary>
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

    /// <summary>视口大小变化时重新约束悬浮控件位置。</summary>
    private void OnViewportSizeChanged()
    {
        if (IsNodeReady())
        {
            ClampDebugToggleButton();
            ClampDebugPanel();
        }
    }

    /// <summary>处理调试面板标题栏拖动。</summary>
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

    /// <summary>限制 DEBUG 按钮始终落在屏幕内。</summary>
    private void ClampDebugToggleButton()
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(FloatingButtonMargin, viewportSize.X - _debugToggleButton.Size.X - FloatingButtonMargin);
        var maxY = Mathf.Max(FloatingButtonMargin, viewportSize.Y - _debugToggleButton.Size.Y - FloatingButtonMargin);

        _debugToggleButton.Position = new Vector2(
            Mathf.Clamp(_debugToggleButton.Position.X, FloatingButtonMargin, maxX),
            Mathf.Clamp(_debugToggleButton.Position.Y, FloatingButtonMargin, maxY));
    }

    /// <summary>限制调试面板始终落在屏幕内。</summary>
    private void ClampDebugPanel()
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(FloatingPanelMargin, viewportSize.X - _debugPanel.Size.X - FloatingPanelMargin);
        var maxY = Mathf.Max(FloatingPanelMargin, viewportSize.Y - _debugPanel.Size.Y - FloatingPanelMargin);

        _debugPanel.Position = new Vector2(
            Mathf.Clamp(_debugPanel.Position.X, FloatingPanelMargin, maxX),
            Mathf.Clamp(_debugPanel.Position.Y, FloatingPanelMargin, maxY));
    }

    /// <summary>根据当前牌桌层数刷新层过滤滑杆。</summary>
    private void SyncLayerInspector()
    {
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = Mathf.Max(0, _boardController.MaxLayer);
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = _boardController.MaxLayer > 0;
        _layerFilterSlider.SetValueNoSignal(_boardController.VisibleMaxLayer);
        RefreshLayerFilterText();
    }

    /// <summary>根据档案目录填充规则 profile 下拉框。</summary>
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

    /// <summary>刷新“&lt;= Lx”层过滤文字。</summary>
    private void RefreshLayerFilterText()
    {
        var visibleLayer = Mathf.RoundToInt((float)_layerFilterSlider.Value);
        _layerFilterValue.Text = $"<= L{visibleLayer}";
    }

    /// <summary>同步顶部 Score / Match 数值。</summary>
    private void SyncStats()
    {
        _scoreValue.Text = _boardController.CurrentScore.ToString();
        _matchValue.Text = _boardController.CurrentMatchCount.ToString();
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

    /// <summary>让牌桌背景闪一下，用于强调一次重绘或刷新。</summary>
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
