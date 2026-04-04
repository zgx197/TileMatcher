using Godot;

namespace TileMatcher.DebugUI;

/// <summary>
/// 首页与关卡页共用的调试面板组件。
/// 页面只负责决定哪些区域可见、按钮触发什么行为，面板本身只承载通用 UI 结构。
/// </summary>
public partial class SharedDebugPanel : Control
{
    /// <summary>遮罩层根节点。</summary>
    public Control OverlayRoot { get; private set; } = null!;

    /// <summary>调试面板主体。</summary>
    public Control PanelRoot { get; private set; } = null!;

    /// <summary>面板标题栏。</summary>
    public Control HeaderRoot { get; private set; } = null!;

    /// <summary>面板标题文本。</summary>
    public Label TitleLabel { get; private set; } = null!;

    /// <summary>面板右上角关闭按钮。</summary>
    public Button CloseButton { get; private set; } = null!;

    /// <summary>顶部提示说明文本。</summary>
    public Label HintLabel { get; private set; } = null!;

    /// <summary>规则档案行容器。</summary>
    public Control ProfileRow { get; private set; } = null!;

    /// <summary>规则档案下拉框。</summary>
    public OptionButton ProfileSelector { get; private set; } = null!;

    /// <summary>规则摘要文本。</summary>
    public Label RulesSummaryLabel { get; private set; } = null!;

    /// <summary>调试状态文本。</summary>
    public Label DebugLabel { get; private set; } = null!;

    /// <summary>层级过滤区域容器。</summary>
    public Control LayerInspector { get; private set; } = null!;

    /// <summary>层级过滤滑杆。</summary>
    public HSlider LayerFilterSlider { get; private set; } = null!;

    /// <summary>层级过滤当前值文本。</summary>
    public Label LayerFilterValueLabel { get; private set; } = null!;

    /// <summary>随机生成和原型关卡按钮行。</summary>
    public Control GenerationButtonsRow { get; private set; } = null!;

    /// <summary>随机生成按钮。</summary>
    public Button GenerateButton { get; private set; } = null!;

    /// <summary>原型关卡按钮。</summary>
    public Button PrototypeButton { get; private set; } = null!;

    /// <summary>跳关区域容器。</summary>
    public Control JumpRow { get; private set; } = null!;

    /// <summary>跳关输入框。</summary>
    public SpinBox JumpLevelInput { get; private set; } = null!;

    /// <summary>跳关确认按钮。</summary>
    public Button JumpButton { get; private set; } = null!;

    /// <summary>救助中心调试操作行。</summary>
    public Control RescueDebugRow { get; private set; } = null!;

    /// <summary>立即刷新救助中心按钮。</summary>
    public Button RefreshRescueCenterButton { get; private set; } = null!;

    /// <summary>金币调试操作行。</summary>
    public Control CoinDebugRow { get; private set; } = null!;

    /// <summary>要追加的金币数量输入框。</summary>
    public SpinBox AddCoinInput { get; private set; } = null!;

    /// <summary>应用金币追加的按钮。</summary>
    public Button AddCoinButton { get; private set; } = null!;

    /// <summary>重置当前关卡辅助次数按钮。</summary>
    public Button ResetCurrentLevelAssistButton { get; private set; } = null!;

    /// <summary>自动消除一对按钮。</summary>
    public Button AutoMatchButton { get; private set; } = null!;

    /// <summary>重置账号数据按钮。</summary>
    public Button ResetProgressButton { get; private set; } = null!;

    /// <summary>初始化共享调试面板的通用节点引用。</summary>
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
    }

    /// <summary>打开调试面板。</summary>
    public void OpenPanel()
    {
        Visible = true;
    }

    /// <summary>关闭调试面板。</summary>
    public void ClosePanel()
    {
        Visible = false;
    }

    /// <summary>设置当前跳关输入框的关卡号。</summary>
    public void SetJumpLevel(int levelNumber)
    {
        JumpLevelInput.Value = Mathf.Max(1, levelNumber);
    }

    /// <summary>读取跳关输入框中的关卡号，并统一规整到合法范围。</summary>
    public int GetJumpLevel()
    {
        return Mathf.Max(1, Mathf.RoundToInt((float)JumpLevelInput.Value));
    }
}
