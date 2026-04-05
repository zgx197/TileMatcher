using Godot;

namespace TileMatcher.DebugUI;

/// <summary>
/// 首页与关卡页共用的调试面板组件。
/// 面板只提供通用节点引用与基础开关，不承载具体业务逻辑。
/// </summary>
public partial class SharedDebugPanel : Control
{
    public Control OverlayRoot { get; private set; } = null!;

    public Control PanelRoot { get; private set; } = null!;

    public Control HeaderRoot { get; private set; } = null!;

    public Label TitleLabel { get; private set; } = null!;

    public Button CloseButton { get; private set; } = null!;

    public Label HintLabel { get; private set; } = null!;

    public Control ProfileRow { get; private set; } = null!;

    public OptionButton ProfileSelector { get; private set; } = null!;

    public Label RulesSummaryLabel { get; private set; } = null!;

    public Label DebugLabel { get; private set; } = null!;

    public Control LogSection { get; private set; } = null!;

    public Label LogPathLabel { get; private set; } = null!;

    public Label LogSummaryLabel { get; private set; } = null!;

    public Button RefreshLogButton { get; private set; } = null!;

    public Button ShowLatestLogButton { get; private set; } = null!;

    public Button ShowErrorLogButton { get; private set; } = null!;

    public Button CopyLogButton { get; private set; } = null!;

    public Button ClearLogsButton { get; private set; } = null!;

    public Button OpenLogDirectoryButton { get; private set; } = null!;

    public TextEdit LogPreview { get; private set; } = null!;

    public Control LayerInspector { get; private set; } = null!;

    public HSlider LayerFilterSlider { get; private set; } = null!;

    public Label LayerFilterValueLabel { get; private set; } = null!;

    public Control GenerationButtonsRow { get; private set; } = null!;

    public Button GenerateButton { get; private set; } = null!;

    public Button PrototypeButton { get; private set; } = null!;

    public Control JumpRow { get; private set; } = null!;

    public SpinBox JumpLevelInput { get; private set; } = null!;

    public Button JumpButton { get; private set; } = null!;

    public Control RescueDebugRow { get; private set; } = null!;

    public Button RefreshRescueCenterButton { get; private set; } = null!;

    public Control CoinDebugRow { get; private set; } = null!;

    public SpinBox AddCoinInput { get; private set; } = null!;

    public Button AddCoinButton { get; private set; } = null!;

    public Button ResetCurrentLevelAssistButton { get; private set; } = null!;

    public Button AutoMatchButton { get; private set; } = null!;

    public Button ResetProgressButton { get; private set; } = null!;

    public override void _Ready()
    {
        OverlayRoot = this;
        PanelRoot = GetNode<Control>("Panel");
        HeaderRoot = GetNode<Control>("Panel/Margin/Stack/Header");
        TitleLabel = GetNode<Label>("Panel/Margin/Stack/Header/Title");
        CloseButton = GetNode<Button>("Panel/Margin/Stack/Header/CloseButton");
        HintLabel = GetNode<Label>("Panel/Margin/Stack/Hint");
        ProfileRow = GetNode<Control>("Panel/Margin/Stack/ProfileRow");
        ProfileSelector = GetNode<OptionButton>("Panel/Margin/Stack/ProfileRow/ProfileSelector");
        RulesSummaryLabel = GetNode<Label>("Panel/Margin/Stack/RulesSummary");
        DebugLabel = GetNode<Label>("Panel/Margin/Stack/DebugLabel");
        LogSection = GetNode<Control>("Panel/Margin/Stack/LogSection");
        LogPathLabel = GetNode<Label>("Panel/Margin/Stack/LogSection/LogPathLabel");
        LogSummaryLabel = GetNode<Label>("Panel/Margin/Stack/LogSection/LogSummaryLabel");
        RefreshLogButton = GetNode<Button>("Panel/Margin/Stack/LogSection/LogButtonsRow/RefreshLogButton");
        ShowLatestLogButton = GetNode<Button>("Panel/Margin/Stack/LogSection/LogButtonsRow/ShowLatestLogButton");
        ShowErrorLogButton = GetNode<Button>("Panel/Margin/Stack/LogSection/LogButtonsRow/ShowErrorLogButton");
        CopyLogButton = GetNode<Button>("Panel/Margin/Stack/LogSection/LogButtonsRow/CopyLogButton");
        ClearLogsButton = GetNode<Button>("Panel/Margin/Stack/LogSection/LogButtonsRow/ClearLogsButton");
        OpenLogDirectoryButton = GetNode<Button>("Panel/Margin/Stack/LogSection/LogButtonsRow/OpenLogDirectoryButton");
        LogPreview = GetNode<TextEdit>("Panel/Margin/Stack/LogSection/LogPreview");
        LayerInspector = GetNode<Control>("Panel/Margin/Stack/LayerInspector");
        LayerFilterSlider = GetNode<HSlider>("Panel/Margin/Stack/LayerInspector/Controls/Slider");
        LayerFilterValueLabel = GetNode<Label>("Panel/Margin/Stack/LayerInspector/Controls/Value");
        GenerationButtonsRow = GetNode<Control>("Panel/Margin/Stack/Buttons");
        GenerateButton = GetNode<Button>("Panel/Margin/Stack/Buttons/ShuffleButton");
        PrototypeButton = GetNode<Button>("Panel/Margin/Stack/Buttons/PrototypeButton");
        JumpRow = GetNode<Control>("Panel/Margin/Stack/JumpRow");
        JumpLevelInput = GetNode<SpinBox>("Panel/Margin/Stack/JumpRow/JumpLevelInput");
        JumpButton = GetNode<Button>("Panel/Margin/Stack/JumpRow/JumpButton");
        RescueDebugRow = GetNode<Control>("Panel/Margin/Stack/RescueDebugRow");
        RefreshRescueCenterButton = GetNode<Button>("Panel/Margin/Stack/RescueDebugRow/RefreshRescueCenterButton");
        CoinDebugRow = GetNode<Control>("Panel/Margin/Stack/CoinDebugRow");
        AddCoinInput = GetNode<SpinBox>("Panel/Margin/Stack/CoinDebugRow/AddCoinInput");
        AddCoinButton = GetNode<Button>("Panel/Margin/Stack/CoinDebugRow/AddCoinButton");
        ResetCurrentLevelAssistButton = GetNode<Button>("Panel/Margin/Stack/ResetCurrentLevelAssistButton");
        AutoMatchButton = GetNode<Button>("Panel/Margin/Stack/AutoMatchButton");
        ResetProgressButton = GetNode<Button>("Panel/Margin/Stack/ResetProgressButton");

        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        JumpLevelInput.MinValue = 1;
        JumpLevelInput.Step = 1;
        AddCoinInput.MinValue = 1;
        AddCoinInput.MaxValue = 99999;
        AddCoinInput.Step = 1;
        AddCoinInput.Value = 10;
        LayerFilterSlider.Step = 1;
        LogPreview.Editable = false;
        LogPreview.ContextMenuEnabled = true;
        LogPreview.HighlightCurrentLine = false;
        LogPreview.WrapMode = TextEdit.LineWrappingMode.Boundary;
    }

    public void OpenPanel()
    {
        Visible = true;
    }

    public void ClosePanel()
    {
        Visible = false;
    }

    public void SetJumpLevel(int levelNumber)
    {
        JumpLevelInput.Value = Mathf.Max(1, levelNumber);
    }

    public int GetJumpLevel()
    {
        return Mathf.Max(1, Mathf.RoundToInt((float)JumpLevelInput.Value));
    }
}
