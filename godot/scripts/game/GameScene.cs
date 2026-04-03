using Godot;
using TileMatcher.Board;

namespace TileMatcher.Game;

public partial class GameScene : Node2D
{
    private BoardController _boardController = null!;
    private CanvasItem _boardBackground = null!;
    private Label _levelValue = null!;
    private Label _scoreValue = null!;
    private Label _matchValue = null!;
    private Label _debugLabel = null!;
    private Button _generateButton = null!;
    private Button _prototypeButton = null!;
    private Label _layerFilterLabel = null!;
    private HSlider _layerFilterSlider = null!;
    private Label _layerFilterValue = null!;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.07f, 0.42f, 0.29f, 1.0f));

        _boardController = GetNode<BoardController>("BoardController");
        _boardBackground = GetNode<CanvasItem>("UI/Root/BoardBackground");
        _levelValue = GetNode<Label>("UI/Root/TopBar/Stats/LevelBox/VBox/Value");
        _scoreValue = GetNode<Label>("UI/Root/TopBar/Stats/ScoreBox/VBox/Value");
        _matchValue = GetNode<Label>("UI/Root/TopBar/Stats/MatchBox/VBox/Value");
        _debugLabel = GetNode<Label>("UI/Root/BottomBar/FooterStack/DebugLabel");
        _generateButton = GetNode<Button>("UI/Root/BottomBar/FooterStack/Buttons/ShuffleButton");
        _prototypeButton = GetNode<Button>("UI/Root/BottomBar/FooterStack/Buttons/HintButton");
        _layerFilterLabel = GetNode<Label>("UI/Root/BottomBar/FooterStack/LayerInspector/Title");
        _layerFilterSlider = GetNode<HSlider>("UI/Root/BottomBar/FooterStack/LayerInspector/Controls/Slider");
        _layerFilterValue = GetNode<Label>("UI/Root/BottomBar/FooterStack/LayerInspector/Controls/Value");

        _levelValue.Text = "1";
        _scoreValue.Text = "0";
        _matchValue.Text = "0";
        _generateButton.Text = "随机生成";
        _prototypeButton.Text = "固定原型";
        _debugLabel.Text = "点击“随机生成”开始观察新的堆叠结构。";
        _layerFilterLabel.Text = "显示层级";
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = 0;
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = false;
        _layerFilterValue.Text = "<= L0";

        _generateButton.Pressed += OnGeneratePressed;
        _prototypeButton.Pressed += OnPrototypePressed;
        _layerFilterSlider.ValueChanged += OnLayerFilterChanged;
        _boardController.BoardGenerated += OnBoardGenerated;

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

    private void SyncLayerInspector()
    {
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = Mathf.Max(0, _boardController.MaxLayer);
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = _boardController.MaxLayer > 0;
        _layerFilterSlider.SetValueNoSignal(_boardController.VisibleMaxLayer);
        RefreshLayerFilterText();
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
