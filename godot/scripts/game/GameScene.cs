using System;
using System.Collections.Generic;
using Godot;
using TileMatcher.App;
using TileMatcher.Board;
using TileMatcher.Config;

namespace TileMatcher.Game;

/// <summary>
/// 当前游戏页主场景控制器。
/// </summary>
/// <remarks>
/// 它负责绑定调试界面，并把用户操作转成对 BoardController 的调用。
/// 这一层不直接实现牌桌规则，只负责把状态呈现出来。
/// </remarks>
public partial class GameScene : Node2D
{
    private const string DefaultLevelCatalogPath = "res://configs/levels/default_levels.tres";
    private const string BuildPackageNameSettingPath = "tilematcher_build/package_name";
    private const string BuildVersionNameSettingPath = "tilematcher_build/version_name";
    private const string BuildVersionCodeSettingPath = "tilematcher_build/version_code";
    private const string BuildOrientationSettingPath = "tilematcher_build/manifest_orientation";
    private const int MaxRestartCountPerLevel = 3;
    private const int MaxHintCountPerLevel = 3;

    /// <summary>
    /// 当游戏页被单独作为主场景运行时，是否在 `_Ready` 后自动加载一局默认牌桌。
    /// 如果它被 AppRoot 作为子页面驱动，则应关闭这个开关并由外层调用 `StartLevel`。
    /// </summary>
    [Export]
    public bool AutoStartPrototype { get; set; } = true;

    /// <summary>
    /// 关卡目录资源。
    /// GameScene 只负责“按关卡号启动”，真正的关卡参数来源都集中在这里。
    /// </summary>
    [Export]
    public LevelCatalog LevelCatalog { get; set; } = null!;
    /// <summary>调试悬浮按钮距离屏幕边缘的安全间距。</summary>
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

    /// <summary>
    /// 顶部短时交互提示。
    /// </summary>
    /// <remarks>
    /// 它不是调试文本，而是面向玩家的轻量反馈层。
    /// 当前主要用于说明：
    /// - 为什么某张牌拖不起来
    /// - 为什么拖过去后仍然不能消除
    /// </remarks>
    private Control _interactionTip = null!;
    private Label _interactionTipLabel = null!;
    private Button _restartButton = null!;
    private Label _restartCountLabel = null!;
    private Button _hintButton = null!;
    private Label _hintCountLabel = null!;
    private Tween? _interactionTipTween;

    /// <summary>调试悬浮层及其内部控件。</summary>
    private Control _debugOverlay = null!;
    private Control _debugPanel = null!;
    private Control _debugHeader = null!;
    private Label _debugLabel = null!;
    private Label _rulesSummaryLabel = null!;
    private Button _backHomeButton = null!;
    private Button _generateButton = null!;
    private Button _prototypeButton = null!;
    /// <summary>调试面板中的跳关输入框。</summary>
    private SpinBox _jumpLevelInput = null!;
    /// <summary>确认跳到指定关卡的按钮。</summary>
    private Button _jumpLevelButton = null!;
    /// <summary>重置当前关卡辅助次数的按钮。</summary>
    private Button _resetCurrentLevelAssistButton = null!;
    /// <summary>触发自动消除一对的调试按钮。</summary>
    private Button _autoMatchButton = null!;
    private Button _resetProgressButton = null!;
    private HSlider _layerFilterSlider = null!;
    private Label _layerFilterValue = null!;
    private Button _debugToggleButton = null!;
    private Button _debugCloseButton = null!;
    private OptionButton _profileSelector = null!;
    private Button _settingsButton = null!;
    private Control _leaveConfirmOverlay = null!;
    private Control _leaveConfirmPanel = null!;
    private Button _leaveConfirmCancelButton = null!;
    private Button _leaveConfirmConfirmButton = null!;

    /// <summary>以下状态用于区分“点击”和“拖动”，避免浮动控件误触。</summary>
    private bool _debugButtonPressed;
    private bool _debugButtonDragged;
    private bool _debugPanelPressed;
    private bool _debugPanelDragged;
    private Vector2 _debugButtonPressPosition;
    private Vector2 _debugButtonStartPosition;
    private Vector2 _debugPanelPressPosition;
    private Vector2 _debugPanelStartPosition;

    /// <summary>当前正在游玩的关卡号。</summary>
    private int _currentLevelNumber = 1;

    /// <summary>当前关卡开始时的毫秒时间戳。</summary>
    private ulong _levelStartTicksMsec;

    /// <summary>当前关卡是否已经触发过通关事件。</summary>
    private bool _levelCompleted;

    /// <summary>由外围流程绑定进来的玩家进度对象。</summary>
    private PlayerProgressData? _progressData;

    /// <summary>由外围流程提供的存档保存回调。</summary>
    private Action? _saveProgressAction;

    /// <summary>单独运行 GameScene 时使用的本地辅助次数缓存。</summary>
    private readonly Dictionary<int, LevelAssistUsageData> _standaloneAssistUsageByLevel = [];

    /// <summary>页面流程层使用的普通 C# 事件，不走 Godot Signal 序列化约束。</summary>
    public event Action<LevelCompleteResult>? LevelCompleted;

    /// <summary>请求返回主页的页面层事件。</summary>
    public event Action? BackToHomeRequested;

    /// <summary>请求从外围流程直接跳转到指定关卡。</summary>
    public event Action<int>? DebugLevelJumpRequested;

    /// <summary>请求清空账号数据并强制返回主页。</summary>
    public event Action? ResetProgressRequested;

    /// <summary>
    /// 绑定外围流程层持有的玩家进度对象。
    /// 游戏页内部只读写辅助资源使用情况，真正的保存动作仍由 AppRoot 统一触发。
    /// </summary>
    public void BindProgressContext(PlayerProgressData progressData, Action saveProgressAction)
    {
        _progressData = progressData;
        _saveProgressAction = saveProgressAction;

        if (IsNodeReady())
        {
            RefreshAssistButtons();
        }
    }

    /// <summary>绑定节点、初始化文本，并接通游戏页交互事件。</summary>
    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.06f, 0.36f, 0.29f, 1.0f));

        _boardController = GetNode<BoardController>("BoardController");
        _boardBackground = GetNode<CanvasItem>("BoardBackdrop/Root/BoardBackground");
        _levelValue = GetNode<Label>("UI/Root/TopBar/Layout/Stats/LevelBox/VBox/Value");
        _scoreValue = GetNode<Label>("UI/Root/TopBar/Layout/Stats/ScoreBox/VBox/Value");
        _matchValue = GetNode<Label>("UI/Root/TopBar/Layout/Stats/MatchBox/VBox/Value");
        _interactionTip = GetNode<Control>("UI/Root/InteractionTip");
        _interactionTipLabel = GetNode<Label>("UI/Root/InteractionTip/Label");
        _restartButton = GetNode<Button>("UI/Root/BottomActions/RestartButton");
        _restartCountLabel = GetNode<Label>("UI/Root/BottomActions/RestartButton/Count/Value");
        _hintButton = GetNode<Button>("UI/Root/BottomActions/HintButton");
        _hintCountLabel = GetNode<Label>("UI/Root/BottomActions/HintButton/Count/Value");
        _debugOverlay = GetNode<Control>("UI/Root/DebugOverlay");
        _debugPanel = GetNode<Control>("UI/Root/DebugOverlay/Panel");
        _debugHeader = GetNode<Control>("UI/Root/DebugOverlay/Panel/Margin/Stack/Header");
        _debugLabel = GetNode<Label>("UI/Root/DebugOverlay/Panel/Margin/Stack/DebugLabel");
        _rulesSummaryLabel = GetNode<Label>("UI/Root/DebugOverlay/Panel/Margin/Stack/RulesSummary");
        _backHomeButton = GetNode<Button>("UI/Root/TopBar/Layout/BackHomeButton");
        _settingsButton = GetNode<Button>("UI/Root/TopBar/Layout/SettingsButton");
        _generateButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/Buttons/ShuffleButton");
        _prototypeButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/Buttons/PrototypeButton");
        _jumpLevelInput = GetNode<SpinBox>("UI/Root/DebugOverlay/Panel/Margin/Stack/JumpRow/JumpLevelInput");
        _jumpLevelButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/JumpRow/JumpButton");
        _resetCurrentLevelAssistButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/ResetCurrentLevelAssistButton");
        _autoMatchButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/AutoMatchButton");
        _resetProgressButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/ResetProgressButton");
        _layerFilterSlider = GetNode<HSlider>("UI/Root/DebugOverlay/Panel/Margin/Stack/LayerInspector/Controls/Slider");
        _layerFilterValue = GetNode<Label>("UI/Root/DebugOverlay/Panel/Margin/Stack/LayerInspector/Controls/Value");
        _debugToggleButton = GetNode<Button>("UI/Root/DebugToggleButton");
        _debugCloseButton = GetNode<Button>("UI/Root/DebugOverlay/Panel/Margin/Stack/Header/CloseButton");
        _profileSelector = GetNode<OptionButton>("UI/Root/DebugOverlay/Panel/Margin/Stack/ProfileRow/ProfileSelector");
        _leaveConfirmOverlay = GetNode<Control>("UI/Root/LeaveConfirmOverlay");
        _leaveConfirmPanel = GetNode<Control>("UI/Root/LeaveConfirmOverlay/Panel");
        _leaveConfirmCancelButton = GetNode<Button>("UI/Root/LeaveConfirmOverlay/Panel/Margin/Stack/Buttons/CancelButton");
        _leaveConfirmConfirmButton = GetNode<Button>("UI/Root/LeaveConfirmOverlay/Panel/Margin/Stack/Buttons/ConfirmButton");

        _levelValue.Text = "1";
        _backHomeButton.Text = "主页";
        _generateButton.Text = "随机生成";
        _prototypeButton.Text = "固定原型";
        _jumpLevelButton.Text = "跳到该关";
        _resetCurrentLevelAssistButton.Text = "重置当前关卡辅助次数";
        _autoMatchButton.Text = "自动消除一对";
        _resetProgressButton.Text = "重置账号数据";
        _debugLabel.Text = "点击牌桌中的可移动麻将，可以先验证基础配对消除逻辑。";
        _rulesSummaryLabel.Text = string.Empty;
        _backHomeButton.Text = "< 返回主页";
        _backHomeButton.Text = "返回主页";
        _backHomeButton.Text = "< 返回主页";
        _backHomeButton.Text = "←";
        _settingsButton.Text = "≡";
        _layerFilterSlider.MinValue = 0;
        _layerFilterSlider.MaxValue = 0;
        _layerFilterSlider.Step = 1;
        _layerFilterSlider.Editable = false;
        _jumpLevelInput.MinValue = 1;
        _jumpLevelInput.Step = 1;
        _jumpLevelInput.Value = _currentLevelNumber;
        _layerFilterValue.Text = "<= L0";
        _debugOverlay.Visible = false;
        _interactionTip.Visible = false;
        _interactionTipLabel.Text = string.Empty;
        _restartButton.Text = "重开";
        _hintButton.Text = "提示";
        _leaveConfirmOverlay.Visible = false;

        EnsureLevelCatalogLoaded();
        _backHomeButton.Pressed += OnBackHomePressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _restartButton.Pressed += OnRestartPressed;
        _hintButton.Pressed += OnHintPressed;
        _generateButton.Pressed += OnGeneratePressed;
        _prototypeButton.Pressed += OnPrototypePressed;
        _jumpLevelButton.Pressed += OnJumpLevelPressed;
        _resetCurrentLevelAssistButton.Pressed += OnResetCurrentLevelAssistPressed;
        _autoMatchButton.Pressed += OnAutoMatchPressed;
        _resetProgressButton.Pressed += OnResetProgressPressed;
        _layerFilterSlider.ValueChanged += OnLayerFilterChanged;
        _debugToggleButton.GuiInput += OnDebugToggleGuiInput;
        _debugHeader.GuiInput += OnDebugPanelGuiInput;
        _debugCloseButton.Pressed += OnDebugClosePressed;
        _profileSelector.ItemSelected += OnProfileSelected;
        _leaveConfirmCancelButton.Pressed += OnLeaveConfirmCancelPressed;
        _leaveConfirmConfirmButton.Pressed += OnLeaveConfirmConfirmPressed;
        _boardController.BoardGenerated += OnBoardGenerated;
        _boardController.BoardStateChanged += OnBoardStateChanged;
        _boardController.InteractionTipRequested += OnInteractionTipRequested;
        GetViewport().SizeChanged += OnViewportSizeChanged;

        InitializeProfileSelector();
        InitializeDebugButtonPosition();
        SyncStats();
        RefreshAssistButtons();
        RefreshDebugPanelSummary();
        if (AutoStartPrototype)
        {
            StartLevel(_currentLevelNumber);
        }
    }

    /// <summary>响应“随机生成”按钮。</summary>
    /// <summary>
    /// 由外围流程显式启动一局关卡。
    /// 第一阶段先复用当前稳定的原型牌桌，确保 Home -> Game -> Result 的页面流转先跑通。
    /// </summary>
    public void StartLevel(int levelNumber)
    {
        _currentLevelNumber = Math.Max(1, levelNumber);
        _levelCompleted = false;
        _levelStartTicksMsec = Time.GetTicksMsec();
        _levelValue.Text = _currentLevelNumber.ToString();
        _jumpLevelInput.Value = _currentLevelNumber;
        RefreshAssistButtons();

        var levelConfig = FindLevelConfig(_currentLevelNumber);
        if (levelConfig is null)
        {
            GD.PushWarning($"[GameScene] 未找到关卡配置，回退到固定原型: level={_currentLevelNumber}");
            _debugLabel.Text = $"关卡 {_currentLevelNumber} 未配置，已回退到固定原型。";
            _boardController.LoadPrototype(_currentLevelNumber, $"关卡 {_currentLevelNumber} 原型布局");
            InitializeProfileSelector();
            return;
        }

        ApplyLevelConfig(levelConfig);
        _debugLabel.Text = $"已进入关卡 {_currentLevelNumber}，当前使用原型牌桌验证外围流程。";
        return;
    }

    /// <summary>响应底部“重开当前关卡”。</summary>
    private void OnRestartPressed()
    {
        var usage = GetCurrentAssistUsage();
        var remainingCount = Math.Max(0, MaxRestartCountPerLevel - usage.RestartUsedCount);
        if (remainingCount <= 0)
        {
            ShowInteractionTip("本关重开次数已用完。");
            return;
        }

        usage.RestartUsedCount += 1;
        PersistProgressContext();
        RefreshAssistButtons();
        PlayButtonFeedback(_restartButton, new Color(0.98f, 0.82f, 0.42f, 1.0f));
        ShowInteractionTip($"已重新开始当前关卡，剩余 {Math.Max(0, MaxRestartCountPerLevel - usage.RestartUsedCount)} 次。");
        StartLevel(_currentLevelNumber);
    }

    /// <summary>响应底部“提示一对可消除麻将”。</summary>
    private void OnHintPressed()
    {
        var usage = GetCurrentAssistUsage();
        var remainingCount = Math.Max(0, MaxHintCountPerLevel - usage.HintUsedCount);
        if (remainingCount <= 0)
        {
            ShowInteractionTip("本关提示次数已用完。");
            return;
        }

        if (!_boardController.TryShowHintPair())
        {
            ShowInteractionTip("当前局面没有可提示的可消除牌。");
            return;
        }

        usage.HintUsedCount += 1;
        PersistProgressContext();
        RefreshAssistButtons();
        PlayButtonFeedback(_hintButton, new Color(1.0f, 0.86f, 0.45f, 1.0f));
        ShowInteractionTip($"已高亮一对可消除麻将，剩余 {Math.Max(0, MaxHintCountPerLevel - usage.HintUsedCount)} 次。");
    }

    private void OnGeneratePressed()
    {
        GD.Print("[GameScene] 点击了随机生成按钮");
        _debugLabel.Text = "正在生成新的随机堆叠结构...";
        HighlightDebugLabel(new Color(0.98f, 0.92f, 0.55f, 1.0f));
        PlayButtonFeedback(_generateButton, new Color(0.95f, 0.78f, 0.32f, 1.0f));
        FlashBoard(new Color(0.20f, 0.54f, 0.40f, 1.0f));
        _boardController.GenerateRandomBoard(_currentLevelNumber, null, $"关卡 {_currentLevelNumber} 调试随机布局");
    }

    /// <summary>响应“固定原型”按钮。</summary>
    private void OnPrototypePressed()
    {
        GD.Print("[GameScene] 点击了固定原型按钮");
        _debugLabel.Text = "正在恢复固定原型布局...";
        HighlightDebugLabel(new Color(0.60f, 0.92f, 0.82f, 1.0f));
        PlayButtonFeedback(_prototypeButton, new Color(0.42f, 0.84f, 0.67f, 1.0f));
        FlashBoard(new Color(0.14f, 0.46f, 0.34f, 1.0f));
        _boardController.LoadPrototype(_currentLevelNumber, $"关卡 {_currentLevelNumber} 调试原型布局");
    }

    /// <summary>响应 debug 面板中的“跳到该关”。</summary>
    private void OnJumpLevelPressed()
    {
        var targetLevel = Math.Max(1, Mathf.RoundToInt((float)_jumpLevelInput.Value));
        _jumpLevelInput.Value = targetLevel;
        PlayButtonFeedback(_jumpLevelButton, new Color(0.74f, 0.90f, 1.0f, 1.0f));
        ShowInteractionTip($"正在跳转到关卡 {targetLevel}...");
        DebugLevelJumpRequested?.Invoke(targetLevel);
    }

    /// <summary>响应 debug 面板中的“重置当前关卡辅助次数”。</summary>
    private void OnResetCurrentLevelAssistPressed()
    {
        var usage = GetCurrentAssistUsage();
        usage.RestartUsedCount = 0;
        usage.HintUsedCount = 0;
        PersistProgressContext();
        RefreshAssistButtons();
        PlayButtonFeedback(_resetCurrentLevelAssistButton, new Color(0.78f, 0.94f, 0.82f, 1.0f));
        ShowInteractionTip("已重置当前关卡的重开和提示次数。");
    }

    /// <summary>响应 debug 面板中的“自动消除一对”。</summary>
    private void OnAutoMatchPressed()
    {
        PlayButtonFeedback(_autoMatchButton, new Color(1.0f, 0.82f, 0.58f, 1.0f));
        if (_boardController.TryAutoRemoveHintPair())
        {
            ShowInteractionTip("已自动消除一对当前可配对的麻将。");
            return;
        }

        ShowInteractionTip("当前局面没有可自动消除的一对麻将。");
    }

    /// <summary>响应 debug 面板中的“重置账号数据”。</summary>
    private void OnResetProgressPressed()
    {
        GD.Print("[GameScene] 点击了重置账号数据按钮");
        PlayButtonFeedback(_resetProgressButton, new Color(1.0f, 0.66f, 0.48f, 1.0f));
        HideLeaveConfirmDialog();
        if (_debugOverlay.Visible)
        {
            ToggleDebugOverlay();
        }

        ShowInteractionTip("正在重置账号数据并返回主页...");
        ResetProgressRequested?.Invoke();
    }

    /// <summary>牌桌生成完成后，同步摘要、统计和调试控件。</summary>
    private void OnBoardGenerated(string summary)
    {
        GD.Print($"[GameScene] 布局生成完成: {summary}");
        SyncLayerInspector();
        SyncStats();
        _debugLabel.Text = _boardController.GetCurrentSummary();
        RefreshDebugPanelSummary();
        HighlightDebugLabel(new Color(0.92f, 0.97f, 0.86f, 1.0f));
        FlashBoard(new Color(0.07f, 0.42f, 0.29f, 1.0f));
    }

    /// <summary>牌桌内部状态变化后，同步顶部统计和调试文本。</summary>
    private void OnBoardStateChanged(string message)
    {
        SyncStats();
        _debugLabel.Text = message;
        RefreshDebugPanelSummary();
        HighlightDebugLabel(new Color(0.82f, 0.90f, 0.99f, 1.0f));
        TryEmitLevelCompleted();
    }

    /// <summary>在顶部中央显示一次短时交互提示。</summary>
    private void OnInteractionTipRequested(string message)
    {
        ShowInteractionTip(message);
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

    /// <summary>处理调试悬浮按钮的点击与拖动。</summary>
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
        RefreshDebugPanelSummary();
    }

    /// <summary>供外围流程在进入关卡后直接展开调试面板。</summary>
    public void OpenDebugOverlay()
    {
        if (_debugOverlay.Visible)
        {
            RefreshDebugPanelSummary();
            return;
        }

        ToggleDebugOverlay();
    }

    /// <summary>显式可见的返回主页入口，避免当前流程只依赖键盘 `Esc`。</summary>
    private void OnBackHomePressed()
    {
        GD.Print("[GameScene] 点击了返回主页按钮");
        PlayButtonFeedback(_backHomeButton, new Color(1.0f, 0.90f, 0.70f, 1.0f));
        ShowLeaveConfirmDialog();
    }

    /// <summary>关闭调试浮窗。</summary>
    /// <summary>顶部设置按钮当前先作为设置/开发入口，点击后打开现有调试面板。</summary>
    private void OnSettingsPressed()
    {
        GD.Print("[GameScene] 点击了顶部设置按钮");
        PlayButtonFeedback(_settingsButton, new Color(1.0f, 0.90f, 0.70f, 1.0f));
        if (!_debugOverlay.Visible)
        {
            ToggleDebugOverlay();
        }
    }

    private void OnDebugClosePressed()
    {
        if (_debugOverlay.Visible)
        {
            ToggleDebugOverlay();
        }
    }

    /// <summary>点击“继续游玩”后关闭离开确认弹窗。</summary>
    private void OnLeaveConfirmCancelPressed()
    {
        PlayButtonFeedback(_leaveConfirmCancelButton, new Color(0.97f, 0.92f, 0.82f, 1.0f));
        HideLeaveConfirmDialog();
    }

    /// <summary>点击“确认离开”后真正返回主页。</summary>
    private void OnLeaveConfirmConfirmPressed()
    {
        PlayButtonFeedback(_leaveConfirmConfirmButton, new Color(1.0f, 0.78f, 0.58f, 1.0f));
        HideLeaveConfirmDialog();
        BackToHomeRequested?.Invoke();
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
        RefreshDebugPanelSummary();
        _debugLabel.Text = $"已切换规则档案：{profile.DisplayName}";
        HighlightDebugLabel(new Color(0.77f, 0.92f, 1.0f, 1.0f));
        _boardController.GenerateRandomBoard(_currentLevelNumber, null, $"关卡 {_currentLevelNumber} 规则切换后随机布局");
    }

    /// <summary>视口大小变化时重新约束悬浮控件位置。</summary>
    private void OnViewportSizeChanged()
    {
        if (IsNodeReady())
        {
            ClampDebugToggleButton();
            ClampDebugPanel();
            RefreshDebugPanelSummary();
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

    /// <summary>初始化调试按钮位置。</summary>
    private void InitializeDebugButtonPosition()
    {
        var viewportSize = GetViewportRect().Size;
        _debugToggleButton.Position = new Vector2(
            viewportSize.X - _debugToggleButton.Size.X - FloatingButtonMargin,
            viewportSize.Y - _debugToggleButton.Size.Y - FloatingButtonMargin);
        ClampDebugToggleButton();
        ClampDebugPanel();
    }

    /// <summary>限制调试按钮始终落在屏幕内。</summary>
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

        RefreshDebugPanelSummary();
    }

    /// <summary>刷新“&lt;= Lx”层过滤文字。</summary>
    private void RefreshLayerFilterText()
    {
        var visibleLayer = Mathf.RoundToInt((float)_layerFilterSlider.Value);
        _layerFilterValue.Text = $"<= L{visibleLayer}";
    }

    /// <summary>显示正式的离开确认弹窗，避免误触后直接中断当前关卡。</summary>
    private void ShowLeaveConfirmDialog()
    {
        _leaveConfirmOverlay.Visible = true;
        _leaveConfirmOverlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _leaveConfirmPanel.PivotOffset = _leaveConfirmPanel.Size * 0.5f;
        _leaveConfirmPanel.Scale = new Vector2(0.94f, 0.94f);
        _leaveConfirmPanel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_leaveConfirmPanel, "scale", Vector2.One, 0.12);
        tween.TweenProperty(_leaveConfirmPanel, "modulate", Colors.White, 0.12);
    }

    /// <summary>关闭离开确认弹窗，恢复正常游戏界面。</summary>
    private void HideLeaveConfirmDialog()
    {
        _leaveConfirmOverlay.Visible = false;
    }

    /// <summary>
    /// 若场景未在 Inspector 中显式绑定关卡目录，则从默认路径回退加载。
    /// 这样 `GameScene` 既能被 AppRoot 驱动，也能单独运行调试。
    /// </summary>
    private void RefreshDebugPanelSummary()
    {
        _rulesSummaryLabel.Text = BuildDebugPanelSummary();
    }

    private string BuildDebugPanelSummary()
    {
        // 这里保留调试面板汇总，统一展示构建信息、当前牌桌状态和当前规则摘要。
        var packageName = ReadProjectSetting(BuildPackageNameSettingPath, "unknown.package");
        var versionName = ReadProjectSetting(BuildVersionNameSettingPath, "0.0.0");
        var versionCode = ReadProjectSetting(BuildVersionCodeSettingPath, "0");
        var manifestOrientation = ReadProjectSetting(BuildOrientationSettingPath, "unspecified");

        var buildSummary = $"构建信息 | 包名 {packageName} | 版本 {versionName} ({versionCode}) | 清单方向 {manifestOrientation}";
        var boardSummary = $"当前牌桌 | 关卡 {_currentLevelNumber} | 剩余 {_boardController.CurrentRemainingTileCount} | 可动 {_boardController.CurrentMovableCount} | 已配对 {_boardController.CurrentMatchCount} | 分数 {_boardController.CurrentScore} | 可见层 <= L{_boardController.VisibleMaxLayer}";
        var rulesSummary = _boardController.GetCurrentRulesSummary();
        var sourceSummary = $"来源信息 | 类型 {_boardController.CurrentSourceKindLabel} | 来源 {_boardController.CurrentSourceName}";
        return $"{buildSummary}\n{boardSummary}\n{sourceSummary}\n{rulesSummary}";
    }

    /// <summary>读取项目设置中的构建信息，并统一转成调试面板可直接展示的字符串。</summary>
    private static string ReadProjectSetting(string settingPath, string fallback)
    {
        if (!ProjectSettings.HasSetting(settingPath))
        {
            return fallback;
        }

        var value = ProjectSettings.GetSetting(settingPath);
        var text = value.AsString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private void EnsureLevelCatalogLoaded()
    {
        if (LevelCatalog is not null)
        {
            return;
        }

        LevelCatalog = GD.Load<LevelCatalog>(DefaultLevelCatalogPath);
        if (LevelCatalog is null)
        {
            GD.PushError($"[GameScene] 无法加载默认关卡目录: {DefaultLevelCatalogPath}");
        }
    }

    /// <summary>按关卡号查找配置，找不到时再按目录默认关卡回退一次。</summary>
    private LevelConfig? FindLevelConfig(int levelNumber)
    {
        return LevelCatalog?.ResolveLevelOrFallback(levelNumber);
    }

    /// <summary>
    /// 把关卡配置真正应用到当前游戏页。
    /// 当前版本会先切换规则档案，再决定是加载原型还是生成正式随机布局。
    /// </summary>
    private void ApplyLevelConfig(LevelConfig levelConfig)
    {
        if (!string.IsNullOrWhiteSpace(levelConfig.LayoutProfileId))
        {
            _boardController.SetLayoutProfile(levelConfig.LayoutProfileId);
        }

        InitializeProfileSelector();

        var sourceName = string.IsNullOrWhiteSpace(levelConfig.SourceNameOverride)
            ? $"关卡 {levelConfig.LevelNumber} {ResolveSourceDisplayName(levelConfig)}"
            : levelConfig.SourceNameOverride;

        _debugLabel.Text = $"已进入关卡 {levelConfig.LevelNumber}：{levelConfig.DisplayName}";

        switch (levelConfig.LayoutSourceMode)
        {
            case LevelLayoutSourceMode.Prototype:
                _boardController.LoadPrototype(levelConfig.LevelNumber, sourceName);
                break;

            case LevelLayoutSourceMode.OfflineJson:
                try
                {
                    if (!string.IsNullOrWhiteSpace(levelConfig.OfflineCatalogJsonPath))
                    {
                        _boardController.LoadOfflineCatalogBoard(levelConfig.OfflineCatalogJsonPath, levelConfig.LevelNumber, sourceName);
                    }
                    else
                    {
                        _boardController.LoadOfflineJsonBoard(levelConfig.OfflineLayoutJsonPath, sourceName);
                    }
                }
                catch (Exception exception)
                {
                    GD.PushError($"[GameScene] 离线关卡加载失败，回退到固定原型: level={levelConfig.LevelNumber}, path={levelConfig.OfflineLayoutJsonPath}, error={exception.Message}");
                    _debugLabel.Text = $"离线关卡加载失败，已回退到固定原型: {levelConfig.LevelNumber}";
                    _boardController.LoadPrototype(levelConfig.LevelNumber, $"关卡 {levelConfig.LevelNumber} 原型布局");
                }
                break;

            case LevelLayoutSourceMode.RandomGenerated:
            default:
                int? seed = levelConfig.UseFixedSeed ? levelConfig.RandomSeed : null;
                _boardController.GenerateRandomBoard(levelConfig.LevelNumber, seed, sourceName);
                break;
        }
    }

    private static string ResolveSourceDisplayName(LevelConfig levelConfig)
    {
        return levelConfig.LayoutSourceMode switch
        {
            LevelLayoutSourceMode.Prototype => "原型布局",
            LevelLayoutSourceMode.RandomGenerated => "随机布局",
            LevelLayoutSourceMode.OfflineJson => "离线关卡",
            _ => "未知布局",
        };
    }

    /// <summary>同步顶部“分数 / 已消除对数”数值。</summary>
    private void SyncStats()
    {
        _scoreValue.Text = _boardController.CurrentScore.ToString();
        _matchValue.Text = _boardController.CurrentMatchCount.ToString();
    }

    /// <summary>刷新底部两个辅助按钮的剩余次数和可点击状态。</summary>
    private void RefreshAssistButtons()
    {
        var usage = GetCurrentAssistUsage();
        var restartRemainingCount = Math.Max(0, MaxRestartCountPerLevel - usage.RestartUsedCount);
        var hintRemainingCount = Math.Max(0, MaxHintCountPerLevel - usage.HintUsedCount);

        _restartCountLabel.Text = restartRemainingCount.ToString();
        _hintCountLabel.Text = hintRemainingCount.ToString();
        _restartButton.Disabled = restartRemainingCount <= 0;
        _hintButton.Disabled = hintRemainingCount <= 0;
        _resetCurrentLevelAssistButton.Disabled = restartRemainingCount == MaxRestartCountPerLevel &&
            hintRemainingCount == MaxHintCountPerLevel;
        _restartButton.Modulate = restartRemainingCount > 0 ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.52f);
        _hintButton.Modulate = hintRemainingCount > 0 ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.52f);
        _resetCurrentLevelAssistButton.Modulate = _resetCurrentLevelAssistButton.Disabled
            ? new Color(1.0f, 1.0f, 1.0f, 0.52f)
            : Colors.White;
    }

    /// <summary>取得当前关卡对应的辅助资源使用状态。</summary>
    private LevelAssistUsageData GetCurrentAssistUsage()
    {
        if (_progressData is not null)
        {
            return _progressData.GetOrCreateLevelAssistUsage(_currentLevelNumber);
        }

        if (!_standaloneAssistUsageByLevel.TryGetValue(_currentLevelNumber, out var usage) || usage is null)
        {
            usage = new LevelAssistUsageData();
            _standaloneAssistUsageByLevel[_currentLevelNumber] = usage;
        }

        return usage;
    }

    /// <summary>把辅助资源使用情况写回外围流程层。</summary>
    private void PersistProgressContext()
    {
        _saveProgressAction?.Invoke();
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

    /// <summary>
    /// 显示顶部交互提示，用于表达“为什么这次拖不动 / 为什么这次不能消除”。
    /// </summary>
    private void ShowInteractionTip(string message)
    {
        _interactionTipTween?.Kill();
        _interactionTip.Visible = true;
        _interactionTipLabel.Text = message;
        _interactionTip.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        var startPosition = new Vector2(_interactionTip.Position.X, 108.0f);
        var settlePosition = new Vector2(_interactionTip.Position.X, 116.0f);
        _interactionTip.Position = startPosition;

        var tween = CreateTween();
        _interactionTipTween = tween;
        tween.SetParallel(true);
        tween.TweenProperty(_interactionTip, "position", settlePosition, 0.12);
        tween.TweenProperty(_interactionTip, "modulate", Colors.White, 0.12);
        tween.Chain().TweenInterval(0.55);
        tween.Chain().TweenProperty(_interactionTip, "modulate", new Color(1.0f, 1.0f, 1.0f, 0.0f), 0.18);
        tween.Finished += () =>
        {
            _interactionTip.Visible = false;
            _interactionTipTween = null;
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            !keyEvent.Echo &&
            keyEvent.Keycode == Key.Escape)
        {
            if (_leaveConfirmOverlay.Visible)
            {
                GD.Print("[GameScene] 收到返回键：关闭离开确认弹窗");
                HideLeaveConfirmDialog();
            }
            else
            {
                GD.Print("[GameScene] 收到返回键：弹出离开确认");
                ShowLeaveConfirmDialog();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// 当棋盘剩余牌数归零时，向外围流程发出一次通关事件。
    /// 这里保持在页面层判断，避免把结算页流转逻辑耦合进 BoardController。
    /// </summary>
    private void TryEmitLevelCompleted()
    {
        if (_levelCompleted)
        {
            return;
        }

        if (_boardController.CurrentRemainingTileCount > 0)
        {
            return;
        }

        _levelCompleted = true;
        var result = new LevelCompleteResult
        {
            LevelNumber = _currentLevelNumber,
            Score = _boardController.CurrentScore,
            MatchCount = _boardController.CurrentMatchCount,
            ElapsedText = BuildElapsedText(),
        };

        GD.Print($"[GameScene] 关卡完成：level={result.LevelNumber}, score={result.Score}, matches={result.MatchCount}, elapsed={result.ElapsedText}");
        LevelCompleted?.Invoke(result);
    }

    /// <summary>
    /// 把本局已用时格式化成结算页可直接显示的文本。
    /// 当前版本先统一使用 mm:ss。
    /// </summary>
    private string BuildElapsedText()
    {
        var elapsedMilliseconds = Time.GetTicksMsec() - _levelStartTicksMsec;
        var elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }
}
