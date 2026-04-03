using Godot;
using TileMatcher.Board;
using TileMatcher.Config;
using TileMatcher.Layout;

namespace TileMatcher.Game;

public partial class GameScene : Node2D
{
    private const float FloatingButtonMargin = 22.0f;
    private const float FloatingPanelMargin = 18.0f;

    private BoardController _boardController = null!;
    private CanvasItem _boardBackground = null!;
    private Label _levelValue = null!;
    private Label _scoreValue = null!;
    private Label _matchValue = null!;
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

    private void OnGeneratePressed()
    {
        GD.Print("[GameScene] 点击了随机生成按钮");
        _debugLabel.Text = "正在生成新的随机堆叠结构...";
        HighlightDebugLabel(new Color(0.98f, 0.92f, 0.55f, 1.0f));
        PlayButtonFeedback(_generateButton, new Color(0.95f, 0.78f, 0.32f, 1.0f));
        FlashBoard(new Color(0.20f, 0.54f, 0.40f, 1.0f));
        _boardController.GenerateRandomBoard();
    }

    private void OnPrototypePressed()
    {
        GD.Print("[GameScene] 点击了固定原型按钮");
        _debugLabel.Text = "正在恢复固定原型布局...";
        HighlightDebugLabel(new Color(0.60f, 0.92f, 0.82f, 1.0f));
        PlayButtonFeedback(_prototypeButton, new Color(0.42f, 0.84f, 0.67f, 1.0f));
        FlashBoard(new Color(0.14f, 0.46f, 0.34f, 1.0f));
        _boardController.LoadPrototype();
    }

    private void OnBoardGenerated(string summary)
    {
        GD.Print($"[GameScene] 布局生成完成: {summary}");
        SyncLayerInspector();
        _debugLabel.Text = _boardController.GetCurrentSummary();
        _rulesSummaryLabel.Text = _boardController.GetCurrentRulesSummary();
        HighlightDebugLabel(new Color(0.92f, 0.97f, 0.86f, 1.0f));
        FlashBoard(new Color(0.07f, 0.42f, 0.29f, 1.0f));
    }

    private void OnLayerFilterChanged(double value)
    {
        var visibleLayer = Mathf.RoundToInt((float)value);
        _boardController.SetVisibleMaxLayer(visibleLayer);
        RefreshLayerFilterText();
        _debugLabel.Text = _boardController.GetCurrentSummary();
        HighlightDebugLabel(new Color(0.82f, 0.90f, 0.99f, 1.0f));
    }

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

    private void ToggleDebugOverlay()
    {
        _debugOverlay.Visible = !_debugOverlay.Visible;
        _debugOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private void OnDebugClosePressed()
    {
        if (_debugOverlay.Visible)
        {
            ToggleDebugOverlay();
        }
    }

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

    private void OnViewportSizeChanged()
    {
        if (IsNodeReady())
        {
            ClampDebugToggleButton();
            ClampDebugPanel();
        }
    }

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

    private void InitializeDebugButtonPosition()
    {
        var viewportSize = GetViewportRect().Size;
        _debugToggleButton.Position = new Vector2(
            viewportSize.X - _debugToggleButton.Size.X - FloatingButtonMargin,
            FloatingButtonMargin);
        ClampDebugToggleButton();
        ClampDebugPanel();
    }

    private void ClampDebugToggleButton()
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(FloatingButtonMargin, viewportSize.X - _debugToggleButton.Size.X - FloatingButtonMargin);
        var maxY = Mathf.Max(FloatingButtonMargin, viewportSize.Y - _debugToggleButton.Size.Y - FloatingButtonMargin);

        _debugToggleButton.Position = new Vector2(
            Mathf.Clamp(_debugToggleButton.Position.X, FloatingButtonMargin, maxX),
            Mathf.Clamp(_debugToggleButton.Position.Y, FloatingButtonMargin, maxY));
    }

    private void ClampDebugPanel()
    {
        var viewportSize = GetViewportRect().Size;
        var maxX = Mathf.Max(FloatingPanelMargin, viewportSize.X - _debugPanel.Size.X - FloatingPanelMargin);
        var maxY = Mathf.Max(FloatingPanelMargin, viewportSize.Y - _debugPanel.Size.Y - FloatingPanelMargin);

        _debugPanel.Position = new Vector2(
            Mathf.Clamp(_debugPanel.Position.X, FloatingPanelMargin, maxX),
            Mathf.Clamp(_debugPanel.Position.Y, FloatingPanelMargin, maxY));
    }

    private void SyncLayerInspector()
    {
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = Mathf.Max(0, _boardController.MaxLayer);
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = _boardController.MaxLayer > 0;
        _layerFilterSlider.SetValueNoSignal(_boardController.VisibleMaxLayer);
        RefreshLayerFilterText();
    }

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

    private void RefreshLayerFilterText()
    {
        var visibleLayer = Mathf.RoundToInt((float)_layerFilterSlider.Value);
        _layerFilterValue.Text = $"<= L{visibleLayer}";
    }

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

    private void FlashBoard(Color targetColor)
    {
        _boardBackground.Modulate = new Color(1.16f, 1.16f, 1.16f, 1.0f);

        var tween = CreateTween();
        tween.TweenProperty(_boardBackground, "modulate", targetColor, 0.28);
    }

    private void HighlightDebugLabel(Color color)
    {
        _debugLabel.Modulate = color;

        var tween = CreateTween();
        tween.TweenProperty(_debugLabel, "modulate", Colors.White, 0.35);
    }
}
