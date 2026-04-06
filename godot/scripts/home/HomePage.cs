using System.Collections.Generic;
using Godot;
using TileMatcher.DebugUI;
using TileMatcher.Pets;

namespace TileMatcher.Home;

/// <summary>
/// 外围流程首页。
/// 当前负责展示首页三段式结构、宠物救助弹窗、宠物乐园以及首页复用的调试面板。
/// </summary>
public partial class HomePage : Control
{
    private const string PetActorScenePath = "res://scenes/home/PetActorView.tscn";
    private const float NarrowWidthBreakpoint = 760.0f;

    /// <summary>首页内容边距容器。</summary>
    private MarginContainer _content = null!;

    /// <summary>宽屏头部布局。</summary>
    private Control _desktopHeader = null!;

    /// <summary>窄屏头部布局。</summary>
    private Control _mobileHeader = null!;

    /// <summary>宽屏品牌标题。</summary>
    private Label _desktopBrandTitleLabel = null!;

    /// <summary>窄屏品牌标题。</summary>
    private Label _mobileBrandTitleLabel = null!;

    /// <summary>宽屏欢迎语。</summary>
    private Label _desktopBrandSubtitleLabel = null!;

    /// <summary>窄屏欢迎语。</summary>
    private Label _mobileBrandSubtitleLabel = null!;

    /// <summary>宽屏金币数量文本。</summary>
    private Label _desktopCoinValueLabel = null!;

    /// <summary>窄屏金币数量文本。</summary>
    private Label _mobileCoinValueLabel = null!;

    /// <summary>宠物乐园区域顶部摘要。</summary>
    private Label _parkSummaryLabel = null!;

    /// <summary>宠物乐园为空时的提示文案。</summary>
    private Label _parkEmptyStateLabel = null!;

    /// <summary>宠物乐园动态列表容器。</summary>
    private Control _petParkList = null!;

    /// <summary>底部玩法标题。</summary>
    private Label _sectionTitleLabel = null!;

    /// <summary>底部进度摘要文本。</summary>
    private Label _progressSummaryLabel = null!;

    /// <summary>底部当前关卡标题。</summary>
    private Label _currentLevelLabel = null!;

    /// <summary>底部当前关卡说明。</summary>
    private Label _levelSummaryLabel = null!;

    /// <summary>宠物救助弹窗主体。</summary>
    private Control _rescuePanel = null!;

    /// <summary>宠物救助弹窗顶部摘要。</summary>
    private Label _rescueSummaryLabel = null!;

    /// <summary>宠物救助弹窗中的反馈文案。</summary>
    private Label _rescueFeedbackLabel = null!;

    /// <summary>宠物救助弹窗中的“本期待救助”标题。</summary>
    private Label _rescueBatchTitleLabel = null!;

    /// <summary>宠物救助弹窗中的本轮刷新摘要。</summary>
    private Label _rescueBatchHintLabel = null!;

    /// <summary>宠物救助弹窗底部说明。</summary>
    private Label _rescueFooterLabel = null!;

    /// <summary>宠物救助动态列表容器。</summary>
    private VBoxContainer _rescueList = null!;

    /// <summary>宽屏宠物救助按钮。</summary>
    private Button _desktopRescueButton = null!;

    /// <summary>窄屏宠物救助按钮。</summary>
    private Button _mobileRescueButton = null!;

    /// <summary>进入关卡的主按钮。</summary>
    private Button _startButton = null!;

    /// <summary>宽屏调试按钮。</summary>
    private Button _desktopDebugButton = null!;

    /// <summary>窄屏调试按钮。</summary>
    private Button _mobileDebugButton = null!;

    /// <summary>宠物救助弹窗根节点。</summary>
    private Control _rescueOverlay = null!;

    /// <summary>点击遮罩关闭宠物救助弹窗的按钮。</summary>
    private Button _rescueDismissButton = null!;

    /// <summary>宠物救助弹窗右上角关闭按钮。</summary>
    private Button _rescueCloseButton = null!;

    /// <summary>首页复用的共享调试面板。</summary>
    private SharedDebugPanel _debugPanel = null!;

    /// <summary>调试面板中的跳关输入框。</summary>
    private SpinBox _jumpLevelInput = null!;

    /// <summary>调试面板中的跳关按钮。</summary>
    private Button _jumpLevelButton = null!;

    /// <summary>调试面板中的救助中心立即刷新按钮。</summary>
    private Button _refreshRescueCenterButton = null!;

    /// <summary>调试面板中的金币追加输入框。</summary>
    private SpinBox _addCoinInput = null!;

    /// <summary>调试面板中的金币追加按钮。</summary>
    private Button _addCoinButton = null!;

    /// <summary>调试面板中的重置当前关卡辅助次数按钮。</summary>
    private Button _resetCurrentLevelAssistButton = null!;

    /// <summary>调试面板中的重置账号数据按钮。</summary>
    private Button _resetProgressButton = null!;

    /// <summary>调试面板关闭按钮。</summary>
    private Button _debugCloseButton = null!;

    /// <summary>单只宠物组件场景。</summary>
    private PackedScene _petActorScene = null!;

    /// <summary>当前首页准备进入的关卡号。</summary>
    private int _levelNumber = 1;

    /// <summary>节点 Ready 前暂存的品牌名。</summary>
    private string _pendingBrandName = "毛球碰碰乐";

    /// <summary>节点 Ready 前暂存的金币数量。</summary>
    private int _pendingCoinCount = 1;

    /// <summary>节点 Ready 前暂存的进度摘要。</summary>
    private string _pendingProgressSummary = "最高解锁 1 关，已完成 0 关";

    /// <summary>节点 Ready 前暂存的关卡标题。</summary>
    private string _pendingLevelTitle = "第 1 关";

    /// <summary>节点 Ready 前暂存的关卡说明。</summary>
    private string _pendingLevelSummary = "关卡信息准备中";

    /// <summary>节点 Ready 前暂存的宠物定义列表。</summary>
    private List<PetDefinition> _pendingPetDefinitions = [];

    /// <summary>节点 Ready 前暂存的已领养宠物列表。</summary>
    private List<OwnedPetData> _pendingOwnedPets = [];

    /// <summary>节点 Ready 前暂存的救助反馈文本。</summary>
    private string _pendingRescueFeedback = string.Empty;

    [Signal]
    public delegate void StartGameRequestedEventHandler(int levelNumber);

    [Signal]
    public delegate void DebugLevelJumpRequestedEventHandler(int levelNumber);

    [Signal]
    public delegate void AdoptPetRequestedEventHandler(string petId);

    [Signal]
    public delegate void RescuePanelRequestedEventHandler();

    [Signal]
    public delegate void RefreshRescueCenterRequestedEventHandler();

    [Signal]
    public delegate void AddCoinRequestedEventHandler(int coinAmount);

    [Signal]
    public delegate void ResetCurrentLevelAssistRequestedEventHandler(int levelNumber);

    [Signal]
    public delegate void ResetProgressRequestedEventHandler();

    /// <summary>绑定节点引用并接通首页按钮事件。</summary>
    public override void _Ready()
    {
        _content = GetNode<MarginContainer>("Root/Content");
        _desktopHeader = GetNode<Control>("Root/Content/MainStack/DesktopHeader");
        _mobileHeader = GetNode<Control>("Root/Content/MainStack/MobileHeader");
        _desktopBrandTitleLabel = GetNode<Label>("Root/Content/MainStack/DesktopHeader/BrandCard/Margin/Stack/BrandText/BrandTitle");
        _mobileBrandTitleLabel = GetNode<Label>("Root/Content/MainStack/MobileHeader/BrandCard/Margin/Stack/BrandText/BrandTitle");
        _desktopBrandSubtitleLabel = GetNode<Label>("Root/Content/MainStack/DesktopHeader/BrandCard/Margin/Stack/BrandText/BrandSubtitle");
        _mobileBrandSubtitleLabel = GetNode<Label>("Root/Content/MainStack/MobileHeader/BrandCard/Margin/Stack/BrandText/BrandSubtitle");
        _desktopCoinValueLabel = GetNode<Label>("Root/Content/MainStack/DesktopHeader/BrandCard/Margin/Stack/CoinChip/CoinRow/CoinValue");
        _mobileCoinValueLabel = GetNode<Label>("Root/Content/MainStack/MobileHeader/BrandCard/Margin/Stack/CoinChip/CoinRow/CoinValue");
        _parkSummaryLabel = GetNode<Label>("Root/Content/MainStack/PetParkCard/Margin/Stack/PetCanvas/Margin/Body/ParkSummary");
        _parkEmptyStateLabel = GetNode<Label>("Root/Content/MainStack/PetParkCard/Margin/Stack/PetCanvas/Margin/Body/EmptyState");
        _petParkList = GetNode<Control>("Root/Content/MainStack/PetParkCard/Margin/Stack/PetCanvas/Margin/Body/PetParkList");
        _sectionTitleLabel = GetNode<Label>("Root/Content/MainStack/BottomCard/Margin/Stack/SectionTitle");
        _progressSummaryLabel = GetNode<Label>("Root/Content/MainStack/BottomCard/Margin/Stack/ProgressSummary");
        _currentLevelLabel = GetNode<Label>("Root/Content/MainStack/BottomCard/Margin/Stack/CurrentLevel");
        _levelSummaryLabel = GetNode<Label>("Root/Content/MainStack/BottomCard/Margin/Stack/LevelSummary");
        _rescuePanel = GetNode<Control>("ModalLayer/RescueOverlay/RescuePanel");
        _rescueSummaryLabel = GetNode<Label>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/Summary");
        _rescueFeedbackLabel = GetNode<Label>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/Feedback");
        _rescueBatchTitleLabel = GetNode<Label>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/BatchTitle");
        _rescueBatchHintLabel = GetNode<Label>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/BatchHint");
        _rescueFooterLabel = GetNode<Label>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/FooterHint");
        _rescueList = GetNode<VBoxContainer>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/Scroll/List");
        _desktopRescueButton = GetNode<Button>("Root/Content/MainStack/DesktopHeader/RescueButton");
        _mobileRescueButton = GetNode<Button>("Root/Content/MainStack/MobileHeader/TopRow/RescueButton");
        _startButton = GetNode<Button>("Root/Content/MainStack/BottomCard/Margin/Stack/StartButton");
        _desktopDebugButton = GetNode<Button>("Root/Content/MainStack/DesktopHeader/DebugButton");
        _mobileDebugButton = GetNode<Button>("Root/Content/MainStack/MobileHeader/TopRow/DebugButton");
        _rescueOverlay = GetNode<Control>("ModalLayer/RescueOverlay");
        _rescueDismissButton = GetNode<Button>("ModalLayer/RescueOverlay/DismissButton");
        _rescueCloseButton = GetNode<Button>("ModalLayer/RescueOverlay/RescuePanel/Margin/Stack/Header/CloseButton");
        _debugPanel = GetNode<SharedDebugPanel>("ModalLayer/DebugPanel");
        _jumpLevelInput = _debugPanel.JumpLevelInput;
        _jumpLevelButton = _debugPanel.JumpButton;
        _refreshRescueCenterButton = _debugPanel.RefreshRescueCenterButton;
        _addCoinInput = _debugPanel.AddCoinInput;
        _addCoinButton = _debugPanel.AddCoinButton;
        _resetCurrentLevelAssistButton = _debugPanel.ResetCurrentLevelAssistButton;
        _resetProgressButton = _debugPanel.ResetProgressButton;
        _debugCloseButton = _debugPanel.CloseButton;
        _petActorScene = GD.Load<PackedScene>(PetActorScenePath);

        _desktopRescueButton.Pressed += OnRescuePressed;
        _mobileRescueButton.Pressed += OnRescuePressed;
        _startButton.Pressed += OnStartPressed;
        _desktopDebugButton.Pressed += OnDebugPressed;
        _mobileDebugButton.Pressed += OnDebugPressed;
        _rescueDismissButton.Pressed += OnRescueClosePressed;
        _rescueCloseButton.Pressed += OnRescueClosePressed;
        _jumpLevelButton.Pressed += OnJumpLevelPressed;
        _refreshRescueCenterButton.Pressed += OnRefreshRescueCenterPressed;
        _addCoinButton.Pressed += OnAddCoinPressed;
        _resetCurrentLevelAssistButton.Pressed += OnResetCurrentLevelAssistPressed;
        _resetProgressButton.Pressed += OnResetProgressPressed;
        _debugCloseButton.Pressed += OnDebugClosePressed;

        ConfigureDebugPanel();
        RefreshTexts();
        UpdateResponsiveLayout();
    }

    /// <summary>监听窗口尺寸变化并重算首页响应式布局。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsNodeReady())
        {
            UpdateResponsiveLayout();
        }
    }

    /// <summary>由外围流程写入首页应显示的品牌、关卡、救助列表和已领养宠物信息。</summary>
    public void Configure(
        int levelNumber,
        string brandName,
        int coinCount,
        string progressSummary,
        string levelTitle,
        string levelSummary,
        IReadOnlyList<PetDefinition> petDefinitions,
        IReadOnlyList<OwnedPetData> ownedPets,
        string rescueFeedback = "")
    {
        _levelNumber = levelNumber;
        _pendingBrandName = brandName;
        _pendingCoinCount = coinCount;
        _pendingProgressSummary = progressSummary;
        _pendingLevelTitle = levelTitle;
        _pendingLevelSummary = levelSummary;
        _pendingPetDefinitions = new List<PetDefinition>(petDefinitions);
        _pendingOwnedPets = new List<OwnedPetData>(ownedPets);
        _pendingRescueFeedback = rescueFeedback ?? string.Empty;

        if (IsNodeReady())
        {
            RefreshTexts();
        }
    }

    /// <summary>打开救助中心弹窗，并确保调试面板处于关闭状态。</summary>
    public void OpenRescueOverlay()
    {
        _debugPanel.ClosePanel();
        _rescueOverlay.Visible = true;
    }

    /// <summary>把共享调试面板裁剪成首页所需的功能集合。</summary>
    private void ConfigureDebugPanel()
    {
        _debugPanel.TitleLabel.Text = "首页调试面板";
        _debugPanel.HintLabel.Text = "首页调试只负责打开面板，不会自动进入关卡。";
        _debugPanel.ProfileRow.Visible = false;
        _debugPanel.RulesSummaryLabel.Visible = false;
        _debugPanel.DebugLabel.Visible = false;
        _debugPanel.LayerInspector.Visible = false;
        _debugPanel.GenerationButtonsRow.Visible = false;
        _debugPanel.AutoMatchButton.Visible = false;
        _debugPanel.ClosePanel();
    }

    /// <summary>把暂存的首页数据刷新到所有控件上。</summary>
    private void RefreshTexts()
    {
        _desktopBrandTitleLabel.Text = _pendingBrandName;
        _mobileBrandTitleLabel.Text = _pendingBrandName;
        _desktopBrandSubtitleLabel.Text = "欢迎回来，去看看今天等待救助的小家伙吧";
        _mobileBrandSubtitleLabel.Text = "欢迎回来，去看看今天等待救助的小家伙吧";
        _desktopCoinValueLabel.Text = $"x{_pendingCoinCount}";
        _mobileCoinValueLabel.Text = $"x{_pendingCoinCount}";
        _progressSummaryLabel.Text = _pendingProgressSummary;
        _currentLevelLabel.Text = $"当前挑战：{_pendingLevelTitle}";
        _levelSummaryLabel.Text = _pendingLevelSummary;
        _startButton.Text = $"进入第 {_levelNumber} 关";
        RefreshPetPark();
        RefreshRescuePanel();
        _jumpLevelInput.Value = _levelNumber;
        UpdateResponsiveLayout();
    }

    /// <summary>根据当前宽度切换桌面/移动布局并同步字号与弹窗宽度。</summary>
    private void UpdateResponsiveLayout()
    {
        var useMobileLayout = Size.X < NarrowWidthBreakpoint;
        _desktopHeader.Visible = !useMobileLayout;
        _mobileHeader.Visible = useMobileLayout;

        var horizontalMargin = useMobileLayout ? 12 : 26;
        var topMargin = useMobileLayout ? 12 : 26;
        var bottomMargin = useMobileLayout ? 16 : 26;
        _content.AddThemeConstantOverride("margin_left", horizontalMargin);
        _content.AddThemeConstantOverride("margin_top", topMargin);
        _content.AddThemeConstantOverride("margin_right", horizontalMargin);
        _content.AddThemeConstantOverride("margin_bottom", bottomMargin);

        _desktopBrandTitleLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 30 : 32);
        _mobileBrandTitleLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 28 : 30);
        _desktopBrandSubtitleLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 15 : 16);
        _mobileBrandSubtitleLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 15 : 16);
        _desktopCoinValueLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 26 : 28);
        _mobileCoinValueLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 26 : 28);
        _sectionTitleLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 22 : 24);
        _currentLevelLabel.AddThemeFontSizeOverride("font_size", useMobileLayout ? 28 : 34);
        _startButton.AddThemeFontSizeOverride("font_size", useMobileLayout ? 34 : 40);

        var modalHorizontalInset = useMobileLayout ? 12.0f : 24.0f;
        var halfWidth = Mathf.Max(140.0f, Mathf.Min(320.0f, (Size.X * 0.5f) - modalHorizontalInset));
        _rescuePanel.OffsetLeft = -halfWidth;
        _rescuePanel.OffsetRight = halfWidth;
    }

    /// <summary>重建宠物乐园区域的宠物组件列表。</summary>
    private void RefreshPetPark()
    {
        ClearChildren(_petParkList);

        if (_pendingOwnedPets.Count == 0)
        {
            _parkSummaryLabel.Text = "你还没有领养宠物。先去宠物救助站带回第一位小伙伴吧。";
            _parkEmptyStateLabel.Visible = true;
            _parkEmptyStateLabel.Text = "乐园目前还是空的。完成关卡赚金币后，就可以领养新的小动物入住这里。";
            return;
        }

        _parkSummaryLabel.Text = $"目前已有 {_pendingOwnedPets.Count} 位小伙伴入住乐园。每只宠物都会在自己的组件里切换生活状态并刷新状态气泡。";
        _parkEmptyStateLabel.Visible = false;

        for (var index = 0; index < _pendingOwnedPets.Count; index++)
        {
            if (_petActorScene is null)
            {
                continue;
            }

            var ownedPet = _pendingOwnedPets[index];
            var definition = ResolvePetDefinition(ownedPet.PetId);
            var actorView = _petActorScene.Instantiate<PetActorView>();
            actorView.Configure(definition, ownedPet, index, _pendingOwnedPets.Count);
            actorView.ZIndex = 10 + index;
            _petParkList.AddChild(actorView);
        }
    }

    /// <summary>刷新救助中心弹窗中的摘要和卡片列表。</summary>
    private void RefreshRescuePanel()
    {
        var adoptedCount = _pendingOwnedPets.Count;
        _rescueSummaryLabel.Text = $"当前金币 x{_pendingCoinCount}，已领养 {adoptedCount} 只小动物。";
        _rescueFeedbackLabel.Visible = !string.IsNullOrWhiteSpace(_pendingRescueFeedback);
        _rescueFeedbackLabel.Text = _pendingRescueFeedback;
        _rescueBatchTitleLabel.Text = "本期待救助";
        _rescueBatchHintLabel.Text = $"以下是本轮刷新出的 {_pendingPetDefinitions.Count} 只待救助小动物。";
        _rescueFooterLabel.Text = "领养成功后会立即扣除金币、写入存档，并让新伙伴直接出现在中间的宠物乐园里。";

        ClearChildren(_rescueList);

        if (_pendingPetDefinitions.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "当前没有可展示的待救助动物，请检查救助中心刷新逻辑或宠物配置。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _rescueList.AddChild(emptyLabel);
            return;
        }

        foreach (var definition in _pendingPetDefinitions)
        {
            _rescueList.AddChild(CreateRescueCard(definition));
        }
    }

    /// <summary>为一只待救助宠物构造一张可交互卡片。</summary>
    private Control CreateRescueCard(PetDefinition definition)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", CreateCardStyle(definition.ColorHex));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        margin.AddChild(row);

        var info = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        info.AddThemeConstantOverride("separation", 4);
        row.AddChild(info);

        var title = new Label
        {
            Text = $"{definition.DisplayName}（{definition.Species}）",
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.Modulate = new Color(0.23f, 0.16f, 0.10f, 1.0f);
        info.AddChild(title);

        var statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 14);
        info.AddChild(statusLabel);

        var description = new Label
        {
            Text = definition.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        description.AddThemeFontSizeOverride("font_size", 15);
        description.Modulate = new Color(0.28f, 0.21f, 0.15f, 0.82f);
        info.AddChild(description);

        var stateLabel = new Label
        {
            Text = $"入住后常见状态：{definition.ParkActivityText}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        stateLabel.AddThemeFontSizeOverride("font_size", 14);
        stateLabel.Modulate = new Color(0.17f, 0.31f, 0.22f, 0.78f);
        info.AddChild(stateLabel);

        var actionButton = new Button
        {
            CustomMinimumSize = new Vector2(150, 54),
        };

        if (HasAdoptedPet(definition.PetId))
        {
            statusLabel.Text = "状态：已被你救助";
            statusLabel.Modulate = new Color(0.25f, 0.49f, 0.31f, 0.95f);
            actionButton.Text = "已领养";
            actionButton.Disabled = true;
        }
        else if (_pendingCoinCount < definition.Cost)
        {
            statusLabel.Text = "状态：本期待救助，当前金币不足";
            statusLabel.Modulate = new Color(0.66f, 0.37f, 0.17f, 0.95f);
            actionButton.Text = $"还差 {definition.Cost - _pendingCoinCount} 金币";
            actionButton.Disabled = true;
        }
        else
        {
            statusLabel.Text = "状态：本期待救助，可立即领养";
            statusLabel.Modulate = new Color(0.17f, 0.42f, 0.27f, 0.95f);
            actionButton.Text = $"领养 · {definition.Cost} 金币";
            actionButton.Pressed += () => EmitSignal(SignalName.AdoptPetRequested, definition.PetId);
        }

        row.AddChild(actionButton);
        return panel;
    }

    /// <summary>判断首页暂存数据中是否已领养某只宠物。</summary>
    private bool HasAdoptedPet(string petId)
    {
        foreach (var ownedPet in _pendingOwnedPets)
        {
            if (ownedPet is not null && ownedPet.PetId == petId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>按 id 解析宠物定义，缺失时返回可展示的兜底定义。</summary>
    private PetDefinition ResolvePetDefinition(string petId)
    {
        foreach (var definition in _pendingPetDefinitions)
        {
            if (definition.PetId == petId)
            {
                return definition;
            }
        }

        return new PetDefinition
        {
            PetId = petId,
            DisplayName = "未命名宠物",
            Species = "小动物",
            Description = "这只宠物的定义暂时缺失。",
            ColorHex = "#D8D8D8",
            ParkActivityText = "安静等待中",
        };
    }

    /// <summary>根据宠物主题色创建救助卡片的样式。</summary>
    private static StyleBoxFlat CreateCardStyle(string colorHex)
    {
        var baseColor = Color.FromString(colorHex, new Color(0.92f, 0.85f, 0.72f, 1.0f));
        return new StyleBoxFlat
        {
            BgColor = baseColor.Lightened(0.12f),
            BorderColor = baseColor.Darkened(0.18f),
            CornerRadiusTopLeft = 24,
            CornerRadiusTopRight = 24,
            CornerRadiusBottomRight = 24,
            CornerRadiusBottomLeft = 24,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ShadowColor = new Color(0, 0, 0, 0.10f),
            ShadowSize = 6,
        };
    }

    /// <summary>清空某个容器下的全部子节点。</summary>
    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>统一关闭首页当前可见的弹窗。</summary>
    private void CloseAllModals()
    {
        _rescueOverlay.Visible = false;
        _debugPanel.ClosePanel();
    }

    /// <summary>转发打开救助面板请求给外围流程。</summary>
    private void OnRescuePressed()
    {
        EmitSignal(SignalName.RescuePanelRequested);
    }

    /// <summary>关闭救助中心弹窗。</summary>
    private void OnRescueClosePressed()
    {
        _rescueOverlay.Visible = false;
    }

    /// <summary>关闭首页弹窗并进入当前选中的关卡。</summary>
    private void OnStartPressed()
    {
        CloseAllModals();
        EmitSignal(SignalName.StartGameRequested, _levelNumber);
    }

    /// <summary>打开首页调试面板。</summary>
    private void OnDebugPressed()
    {
        _rescueOverlay.Visible = false;
        _debugPanel.OpenPanel();
        _debugPanel.SetJumpLevel(_levelNumber);
    }

    /// <summary>读取调试输入框并发出跳关请求。</summary>
    private void OnJumpLevelPressed()
    {
        var targetLevel = Mathf.Max(1, Mathf.RoundToInt((float)_jumpLevelInput.Value));
        _jumpLevelInput.Value = targetLevel;
        EmitSignal(SignalName.DebugLevelJumpRequested, targetLevel);
    }

    /// <summary>发出立即刷新救助中心请求。</summary>
    private void OnRefreshRescueCenterPressed()
    {
        EmitSignal(SignalName.RefreshRescueCenterRequested);
    }

    /// <summary>读取金币输入框并发出加金币请求。</summary>
    private void OnAddCoinPressed()
    {
        var coinAmount = Mathf.Max(1, Mathf.RoundToInt((float)_addCoinInput.Value));
        _addCoinInput.Value = coinAmount;
        EmitSignal(SignalName.AddCoinRequested, coinAmount);
    }

    /// <summary>重置当前调试关卡的辅助次数。</summary>
    private void OnResetCurrentLevelAssistPressed()
    {
        var targetLevel = Mathf.Max(1, Mathf.RoundToInt((float)_jumpLevelInput.Value));
        _jumpLevelInput.Value = targetLevel;
        EmitSignal(SignalName.ResetCurrentLevelAssistRequested, targetLevel);
    }

    /// <summary>转发重置整个账号进度的请求。</summary>
    private void OnResetProgressPressed()
    {
        EmitSignal(SignalName.ResetProgressRequested);
    }

    /// <summary>关闭首页调试面板。</summary>
    private void OnDebugClosePressed()
    {
        _debugPanel.ClosePanel();
    }
}
