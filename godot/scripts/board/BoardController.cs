using Godot;
using System.Collections.Generic;
using System.Linq;
using TileMatcher.Config;
using TileMatcher.Data;
using TileMatcher.Grid;
using TileMatcher.Layout;
using TileMatcher.Tile;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Board;

/// <summary>
/// 负责把布局数据转换成可见牌桌，并维护当前局内的基础交互状态。
/// </summary>
/// <remarks>
/// 当前版本已经支持两套输入结果：
/// 1. 纯点击：选中一张可移动牌，再点一张同牌面可移动牌后消除。
/// 2. 最小拖拽：按下可移动牌并拖动，松手时如果碰到另一张同牌面可移动牌，则直接消除。
/// 
/// 这里有意不在每帧打印日志，只在“按下 / 开始拖拽 / 结束拖拽 / 命中目标 / 消除完成”这些关键节点输出。
/// 这样能保持日志足够可读，适合快速排查交互链路。
/// </remarks>
public partial class BoardController : Node2D
{
    /// <summary>当场景未显式绑定档案目录时，回退到这份默认资源。</summary>
    private const string DefaultCatalogPath = "res://configs/layout_profiles/default_catalog.tres";

    /// <summary>场景未显式绑定消除反馈配置时，回退使用这份默认资源。</summary>
    private const string DefaultMatchFeedbackPath = "res://configs/effects/default_match_feedback.tres";

    /// <summary>棋盘左右留白，避免牌桌贴边。</summary>
    private const float SidePadding = 36.0f;

    /// <summary>顶部状态栏预留高度。</summary>
    private const float TopReservedHeight = 132.0f;

    /// <summary>底部调试区域和留白预留高度。</summary>
    private const float BottomReservedHeight = 172.0f;

    /// <summary>不同层之间的基础 ZIndex 步长，保证层级永远优先于行列顺序。</summary>
    private const int LayerZStride = 128;

    /// <summary>同层内部按行列细分的 ZIndex 步长。</summary>
    private const int RowZStride = 8;

    /// <summary>按下后移动超过该阈值，才正式认定为拖拽。</summary>
    private const float DragStartThreshold = 12.0f;

    /// <summary>拖拽中给牌的临时抬层增量。</summary>
    private const int DragLiftZBoost = 512;

    /// <summary>拖拽时允许设置的安全最大 ZIndex，避免触发 Godot CanvasItem 上限报错。</summary>
    private const int SafeMaxDragZIndex = 4000;

    /// <summary>拖拽命中判定时，允许主体矩形向外扩张的接触容差。</summary>
    private const float DragContactTolerance = 28.0f;

    /// <summary>两张牌先移动到碰撞预备位的动画时长。</summary>
    private const double MatchStageMoveDuration = 0.24;

    /// <summary>在预备位短暂停顿的时间，让碰撞更有仪式感。</summary>
    private const double MatchStageHoldDuration = 0.08;

    /// <summary>从预备位飞向中点并碰撞的动画时长。</summary>
    private const double MatchCrashDuration = 0.28;

    /// <summary>碰撞后淡出和缩放动画时长。</summary>
    private const double MatchFadeDuration = 0.24;

    /// <summary>两张牌在碰撞前保留的左右间距。</summary>
    private const float MatchStageGapFactor = 0.96f;

    /// <summary>预备位整体向上悬浮的视觉抬升量。</summary>
    private const float MatchStageLiftHeight = 36.0f;

    /// <summary>碰撞瞬间两张牌共同放大的峰值倍率。</summary>
    private const float MatchImpactScale = 1.12f;

    /// <summary>分数弹字持续时间。</summary>
    private const double ScorePopupDuration = 0.82;

    /// <summary>当前牌桌上真实存在的全部牌视图实例。</summary>
    private readonly List<TileView> _tileViews = [];

    /// <summary>当前运行时规则对象。所有布局生成、校验与交互判定都以它为准。</summary>
    private LayoutRules _layoutRules = new();

    /// <summary>当前规则档案的稳定 id。</summary>
    private string _profileId = string.Empty;

    /// <summary>当前规则档案的显示名，用于 UI 和日志。</summary>
    private string _profileDisplayName = "未配置";

    /// <summary>当前已经应用到牌桌上的布局数据。</summary>
    private LevelLayout? _currentLayout;

    /// <summary>当前布局来源，用于调试摘要区分“固定原型 / 随机布局”。</summary>
    private string _currentSourceName = "未加载";

    /// <summary>调试层过滤当前允许显示到哪一层。</summary>
    private int _visibleMaxLayer;

    /// <summary>点击配对模式下当前选中的第一张牌。</summary>
    private TileView? _selectedTile;

    /// <summary>本局累计成功消除的对数。</summary>
    private int _matchCount;

    /// <summary>本局当前分数。当前阶段先使用固定加分模型。</summary>
    private int _score;

    /// <summary>当前鼠标按下时命中的牌。只有在松手前一直保留，便于区分点击和拖拽。</summary>
    private TileView? _pressedTile;

    /// <summary>当前正在被拖拽的牌。只有在真正越过拖拽阈值后才会赋值。</summary>
    private TileView? _draggingTile;

    /// <summary>鼠标按下时的屏幕坐标，用于计算是否超过拖拽阈值。</summary>
    private Vector2 _pointerPressScreenPosition;

    /// <summary>被拖拽牌开始拖拽前的全局位置。拖拽失败后需要回到这个位置。</summary>
    private Vector2 _dragTileStartPosition;

    /// <summary>鼠标指针与牌左上角之间的偏移量，保证拖拽时牌不会瞬间跳到鼠标原点。</summary>
    private Vector2 _dragPointerOffset;

    /// <summary>拖拽前的原始 ZIndex。拖拽结束后必须恢复。</summary>
    private int _dragTileOriginalZIndex;

    /// <summary>消除动画播放期间暂时锁定交互，避免同一时刻重复输入。</summary>
    private bool _interactionLocked;

    [Export]
    public PackedScene TileScene { get; set; } = null!;

    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    /// <summary>
    /// 消除反馈资源。
    /// 这里集中管理拖拽容差、三段动画、白色爆点和分数弹跳等“可调手感参数”，
    /// 避免这些表现层数值继续散落在交互逻辑代码里。
    /// </summary>
    [Export]
    public MatchFeedbackConfig MatchFeedbackConfig { get; set; } = null!;

    [Signal]
    public delegate void BoardGeneratedEventHandler(string summary);

    [Signal]
    public delegate void BoardStateChangedEventHandler(string message);

    /// <summary>当前布局中的最大层号，不等于实际层数。</summary>
    public int MaxLayer => _currentLayout?.Tiles.Count > 0 ? _currentLayout.Tiles.Max(tile => tile.GZ) : 0;

    /// <summary>当前调试过滤允许显示的最高层号。</summary>
    public int VisibleMaxLayer => _visibleMaxLayer;

    /// <summary>当前规则档案 id。</summary>
    public string CurrentProfileId => _profileId;

    /// <summary>当前规则档案显示名。</summary>
    public string CurrentProfileDisplayName => _profileDisplayName;

    /// <summary>当前运行时规则对象。</summary>
    public LayoutRules CurrentRules => _layoutRules;

    /// <summary>当前分数。</summary>
    public int CurrentScore => _score;

    /// <summary>当前已完成的配对数。</summary>
    public int CurrentMatchCount => _matchCount;

    /// <summary>当前仍可移动的牌数。主要用于调试摘要和状态栏。</summary>
    public int CurrentMovableCount => GetActiveTiles().Count(tile => tile.Movable);

    public override void _Ready()
    {
        if (TileScene is null)
        {
            TileScene = GD.Load<PackedScene>("res://scenes/tile/Tile.tscn");
        }

        EnsureProfileCatalogLoaded();
        EnsureMatchFeedbackLoaded();
        EnsureDefaultProfileSelected();
    }

    /// <summary>
    /// 统一处理牌桌级别的鼠标输入。
    /// </summary>
    /// <remarks>
    /// 当前只处理左键按下、左键松开和鼠标移动。
    /// 这样可以用一套状态机同时支撑：
    /// - 点击配对
    /// - 最小拖拽交互
    /// </remarks>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_currentLayout is null)
        {
            return;
        }

        if (_interactionLocked)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left:
                HandleLeftMouseButton(mouseButton);
                break;

            case InputEventMouseMotion mouseMotion:
                HandleMouseMotion(mouseMotion);
                break;
        }
    }

    /// <summary>加载固定原型布局。</summary>
    public void LoadPrototype()
    {
        ApplyLayout(PrototypeLayoutFactory.CreateSingleLevelPrototype(_layoutRules), "固定原型");
    }

    /// <summary>按当前规则生成一份随机布局。</summary>
    public void GenerateRandomBoard()
    {
        ApplyLayout(RandomStackLayoutGenerator.Generate(1, _layoutRules), "随机布局");
    }

    /// <summary>切换当前规则档案。</summary>
    public void SetLayoutProfile(string profileId)
    {
        var profile = FindProfile(profileId);
        if (profile is null)
        {
            GD.PushWarning($"[BoardController] 未找到规则档案: {profileId}");
            return;
        }

        _profileId = profile.ProfileId;
        _profileDisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.ProfileId : profile.DisplayName;
        _layoutRules = profile.ToRuntimeRules();

        LogBoard($"切换规则档案: id={_profileId}, name={_profileDisplayName}");
    }

    /// <summary>设置调试过滤允许显示到哪一层。</summary>
    public void SetVisibleMaxLayer(int visibleMaxLayer)
    {
        if (_currentLayout is null)
        {
            _visibleMaxLayer = 0;
            return;
        }

        _visibleMaxLayer = Mathf.Clamp(visibleMaxLayer, 0, MaxLayer);
        LogBoard($"调整可见层过滤: visible_max_layer={_visibleMaxLayer}");
        RefreshVisibleLayers();
    }

    /// <summary>构造当前牌桌摘要文本。</summary>
    public string GetCurrentSummary()
    {
        return _currentLayout is null
            ? "尚未加载布局"
            : BuildSummary(_currentLayout, _currentSourceName, _visibleMaxLayer, _score, _matchCount, CurrentMovableCount);
    }

    /// <summary>构造当前规则摘要文本。</summary>
    public string GetCurrentRulesSummary()
    {
        return $"规则配置 | 档案 {CurrentProfileDisplayName} | 牌形 {CurrentRules.TileWidthUnits}x{CurrentRules.TileHeightUnits} | 上层偏移 {CurrentRules.GetOffsetModeDisplayName()} | 层数 {CurrentRules.MinLayerCount}-{CurrentRules.MaxLayerCount} | 底层至少 {CurrentRules.MinBottomLayerTileCount} | 完整覆盖 {(CurrentRules.RequireStrictSupport ? "开" : "关")} | 上层收缩 {(CurrentRules.RequireUpperLayerStrictlySmaller ? "开" : "关")}";
    }

    /// <summary>返回当前档案目录中的全部规则档案。</summary>
    public LayoutProfileConfig[] GetProfiles()
    {
        if (ProfileCatalog is null)
        {
            return [];
        }

        return ProfileCatalog.Profiles
            .Where(static profile => profile is not null)
            .ToArray()!;
    }

    /// <summary>
    /// 把一份布局真正应用到当前牌桌。
    /// </summary>
    /// <remarks>
    /// 即使校验失败，当前阶段也仍然允许继续渲染，方便直接观察错误布局。
    /// 因此“看得见牌桌”不等于“布局一定合法”。
    /// </remarks>
    private void ApplyLayout(LevelLayout layout, string sourceName)
    {
        _currentLayout = layout;
        _currentSourceName = sourceName;
        _selectedTile = null;
        _matchCount = 0;
        _score = 0;
        ResetPointerState();

        LogBoard($"开始应用布局: source={sourceName}, profile={CurrentProfileDisplayName}, tiles={layout.Tiles.Count}");

        var validation = LayoutValidator.Validate(layout, _layoutRules);
        if (!validation.IsValid)
        {
            GD.PushError($"[BoardController] 布局违反业务规则: {string.Join(" | ", validation.Errors)}");
        }

        var existingChildren = new List<Node>();
        foreach (Node child in GetChildren())
        {
            existingChildren.Add(child);
        }

        foreach (var child in existingChildren)
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _tileViews.Clear();

        var boardOrigin = ComputeCenteredOrigin(layout);
        LogBoard($"布局世界原点: ({boardOrigin.X:0.##}, {boardOrigin.Y:0.##})");

        var drawOrder = layout.Tiles
            .OrderBy(tile => tile.GZ)
            .ThenBy(tile => tile.GY)
            .ThenBy(tile => tile.GX)
            .ToList();

        foreach (var tileData in drawOrder)
        {
            tileData.Removed = false;
            tileData.Movable = false;

            var tile = TileScene.Instantiate<TileView>();
            tile.ApplyData(tileData);
            tile.Position = GridMath.GridToWorld(tileData.GX, tileData.GY, tileData.GZ, boardOrigin);
            tile.ZIndex = tileData.GZ * LayerZStride + tileData.GY * RowZStride + tileData.GX;

            AddChild(tile);
            _tileViews.Add(tile);
        }

        _visibleMaxLayer = MaxLayer;
        RefreshTileStates();
        RefreshVisibleLayers();

        var summary = GetCurrentSummary();
        LogBoard($"布局应用完成: {summary}");
        EmitSignal(SignalName.BoardGenerated, summary);
        EmitSignal(SignalName.BoardStateChanged, $"牌桌已生成：{summary}");
    }

    /// <summary>分派左键按下和松开事件。</summary>
    private void HandleLeftMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.Pressed)
        {
            OnLeftPressed(mouseButton.Position);
        }
        else
        {
            OnLeftReleased(mouseButton.Position);
        }
    }

    /// <summary>
    /// 记录一次新的按下起点。
    /// </summary>
    /// <remarks>
    /// 按下时只做命中记录，不立即进入拖拽。
    /// 这样可以把“点击”和“拖拽”统一到同一条状态机里。
    /// </remarks>
    private void OnLeftPressed(Vector2 screenPosition)
    {
        LogBoard($"收到左键按下: screen=({screenPosition.X:0.##}, {screenPosition.Y:0.##})");

        ResetPointerState();

        var clickedTile = PickTopTileAtScreenPoint(screenPosition);
        if (clickedTile is null)
        {
            LogBoard("按下时未命中任何可见麻将");
            ClearSelection(false);
            return;
        }

        _pressedTile = clickedTile;
        _pointerPressScreenPosition = screenPosition;
        _dragTileStartPosition = clickedTile.GlobalPosition;
        _dragPointerOffset = clickedTile.GlobalPosition - screenPosition;

        LogBoard($"按下命中麻将: {DescribeTile(clickedTile.Data)}, movable={clickedTile.Data.Movable}, z_index={clickedTile.ZIndex}");
        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// 处理左键松开。
    /// </summary>
    /// <remarks>
    /// 如果当前已经处于拖拽态，则按拖拽结束处理。
    /// 否则按点击处理，这样可以自然兼容点击配对。
    /// </remarks>
    private void OnLeftReleased(Vector2 screenPosition)
    {
        if (_draggingTile is not null)
        {
            FinishDrag(screenPosition);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_pressedTile is not null)
        {
            LogBoard($"按下后未进入拖拽，按点击处理: {DescribeTile(_pressedTile.Data)}");
            HandleTileClicked(_pressedTile);
            ResetPointerState();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// 处理鼠标移动。
    /// </summary>
    /// <remarks>
    /// 只有在已经按下某张牌并且超过拖拽阈值后，才真正进入拖拽逻辑。
    /// </remarks>
    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (_pressedTile is null)
        {
            return;
        }

        if (_draggingTile is null)
        {
            var distance = mouseMotion.Position.DistanceTo(_pointerPressScreenPosition);
            if (distance < DragStartThreshold)
            {
                return;
            }

            TryBeginDrag(_pressedTile, mouseMotion.Position);
            if (_draggingTile is null)
            {
                return;
            }
        }

        UpdateDrag(mouseMotion.Position);
        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// 尝试从“按下候选态”切换到“真实拖拽态”。
    /// </summary>
    private void TryBeginDrag(TileView tileView, Vector2 screenPosition)
    {
        if (!tileView.Data.Movable)
        {
            LogBoard($"尝试开始拖拽失败，麻将不可移动: {DescribeTile(tileView.Data)}");
            return;
        }

        _draggingTile = tileView;
        _dragTileOriginalZIndex = tileView.ZIndex;
        var boostedZIndex = Mathf.Min(tileView.ZIndex + DragLiftZBoost, SafeMaxDragZIndex);
        tileView.ZIndex = boostedZIndex;
        tileView.Scale = new Vector2(1.04f, 1.04f);

        LogBoard($"开始拖拽: {DescribeTile(tileView.Data)}, press=({_pointerPressScreenPosition.X:0.##}, {_pointerPressScreenPosition.Y:0.##}), current=({screenPosition.X:0.##}, {screenPosition.Y:0.##}), z={_dragTileOriginalZIndex}->{boostedZIndex}");
    }

    /// <summary>根据当前鼠标位置更新被拖拽牌的位置。</summary>
    private void UpdateDrag(Vector2 screenPosition)
    {
        if (_draggingTile is null)
        {
            return;
        }

        _draggingTile.GlobalPosition = screenPosition + _dragPointerOffset;
    }

    /// <summary>
    /// 结束一次拖拽并尝试寻找可消除目标。
    /// </summary>
    /// <remarks>
    /// 当前实现非常保守：
    /// - 先把牌还原到原位
    /// - 再根据拖拽过程中的最终相交结果决定是否消除
    /// 这样能保证即使目标判定失败，也不会把牌留在错误位置。
    /// </remarks>
    private void FinishDrag(Vector2 screenPosition)
    {
        if (_draggingTile is null)
        {
            return;
        }

        var draggedTile = _draggingTile;
        var targetTile = FindDragMatchTarget(draggedTile);

        LogBoard($"结束拖拽: dragged={DescribeTile(draggedTile.Data)}, release=({screenPosition.X:0.##}, {screenPosition.Y:0.##}), target={(targetTile is null ? "none" : DescribeTile(targetTile.Data))}");

        draggedTile.GlobalPosition = _dragTileStartPosition;
        draggedTile.ZIndex = _dragTileOriginalZIndex;
        draggedTile.Scale = Vector2.One;

        ResetPointerState();

        if (targetTile is null)
        {
            EmitSignal(SignalName.BoardStateChanged, $"拖拽结束，{draggedTile.Data.Type} 没有碰到可消除目标");
            return;
        }

        LogBoard($"拖拽命中目标，开始消除动画: dragged={DescribeTile(draggedTile.Data)}, target={DescribeTile(targetTile.Data)}");
        RemoveMatchedPair(draggedTile, targetTile);
    }

    /// <summary>
    /// 为被拖拽牌查找一个可作为消除目标的牌。
    /// </summary>
    /// <remarks>
    /// 这里刻意允许跨层命中。
    /// 也就是说，只要另一张牌当前仍然可见、未移除、同牌面，并且主体矩形在容差范围内发生接触，
    /// 就允许被视为一次“触碰到目标”。
    ///
    /// 这样做是为了贴近当前参考玩法中的观感：
    /// - 被拖拽牌可以去触碰其他层仍然露出的同牌面麻将
    /// - 不强制要求目标牌本身也必须是可拖拽状态
    ///
    /// 当前的“触碰”不是严格矩形相交，而是：
    /// - 先把被拖拽牌矩形按容差向外扩张
    /// - 再检测是否与目标牌主体矩形相交
    /// 这样拖拽手感会更接近参考游戏，不需要把两张牌完全压进彼此内部。
    /// </remarks>
    private TileView? FindDragMatchTarget(TileView draggedTile)
    {
        var draggedRect = draggedTile.GetGlobalRect();

        return _tileViews
            .Where(tileView => tileView != draggedTile)
            .Where(tileView => tileView.Visible && !tileView.Data.Removed)
            .Where(tileView => tileView.Data.Type == draggedTile.Data.Type)
            .Where(tileView => IsWithinContactTolerance(draggedRect, tileView.GetGlobalRect()))
            .OrderBy(tileView => GetRectGapDistance(draggedRect, tileView.GetGlobalRect()))
            .ThenByDescending(tileView => tileView.ZIndex)
            .FirstOrDefault();
    }

    /// <summary>
    /// 统一刷新整桌牌的可动状态和选中视觉。
    /// </summary>
    private void RefreshTileStates()
    {
        if (_currentLayout is null)
        {
            return;
        }

        var activeTiles = GetActiveTiles();
        foreach (var tile in activeTiles)
        {
            tile.Movable = IsTileMovable(tile, activeTiles);
        }

        LogBoard($"刷新可动状态: active={activeTiles.Count}, movable={activeTiles.Count(tile => tile.Movable)}, selected={(_selectedTile is null ? "none" : DescribeTile(_selectedTile.Data))}");

        foreach (var tileView in _tileViews)
        {
            if (tileView.Data.Removed)
            {
                continue;
            }

            tileView.SetInteractionState(tileView.Data.Movable, tileView == _selectedTile);
        }
    }

    /// <summary>按当前层过滤刷新每张牌的可见性。</summary>
    private void RefreshVisibleLayers()
    {
        foreach (var tileView in _tileViews)
        {
            tileView.Visible = !tileView.Data.Removed && tileView.Data.GZ <= _visibleMaxLayer;
        }
    }

    /// <summary>
    /// 判断一张牌当前是否允许被移动。
    /// </summary>
    /// <remarks>
    /// 这是当前阶段的一版保守可动判定，不追求最终玩法复杂度，
    /// 但足够稳定、足够容易调试。
    /// </remarks>
    private static bool IsTileMovable(AppTileData tile, IReadOnlyCollection<AppTileData> activeTiles)
    {
        if (tile.Removed)
        {
            return false;
        }

        if (GridMath.HasAnyAboveOverlap(tile, activeTiles))
        {
            return false;
        }

        var blockedLeftRight = GridMath.HasLeftNeighbor(tile, activeTiles) && GridMath.HasRightNeighbor(tile, activeTiles);
        if (blockedLeftRight)
        {
            return false;
        }

        var blockedTopBottom = GridMath.HasTopNeighbor(tile, activeTiles) && GridMath.HasBottomNeighbor(tile, activeTiles);
        return !blockedTopBottom;
    }

    /// <summary>
    /// 处理纯点击模式下的牌交互。
    /// </summary>
    private void HandleTileClicked(TileView tileView)
    {
        var activeTiles = GetActiveTiles();
        var hasAboveOverlap = GridMath.HasAnyAboveOverlap(tileView.Data, activeTiles);
        var hasLeftNeighbor = GridMath.HasLeftNeighbor(tileView.Data, activeTiles);
        var hasRightNeighbor = GridMath.HasRightNeighbor(tileView.Data, activeTiles);
        var hasTopNeighbor = GridMath.HasTopNeighbor(tileView.Data, activeTiles);
        var hasBottomNeighbor = GridMath.HasBottomNeighbor(tileView.Data, activeTiles);

        LogBoard(
            $"处理点击: {DescribeTile(tileView.Data)}, movable={tileView.Data.Movable}, " +
            $"above={hasAboveOverlap}, left={hasLeftNeighbor}, right={hasRightNeighbor}, top={hasTopNeighbor}, bottom={hasBottomNeighbor}");

        if (!tileView.Data.Movable)
        {
            LogBoard($"麻将不可移动，交互结束: {DescribeTile(tileView.Data)}");
            EmitSignal(SignalName.BoardStateChanged, $"牌 {tileView.Data.Type} 已被卡住，当前不可移动");
            return;
        }

        if (_selectedTile is null)
        {
            _selectedTile = tileView;
            RefreshTileStates();
            LogBoard($"首次选中麻将: {DescribeTile(tileView.Data)}");
            EmitSignal(SignalName.BoardStateChanged, $"已选中 {tileView.Data.Type}，请再点一张可移动的同牌面麻将");
            return;
        }

        if (_selectedTile == tileView)
        {
            ClearSelection(true);
            LogBoard($"再次点击同一麻将，取消选中: {DescribeTile(tileView.Data)}");
            EmitSignal(SignalName.BoardStateChanged, $"已取消选择 {tileView.Data.Type}");
            return;
        }

        if (_selectedTile.Data.Type != tileView.Data.Type)
        {
            LogBoard($"牌面不同，切换选中目标: old={DescribeTile(_selectedTile.Data)}, new={DescribeTile(tileView.Data)}");
            _selectedTile = tileView;
            RefreshTileStates();
            EmitSignal(SignalName.BoardStateChanged, $"改为选中 {tileView.Data.Type}，不同牌面不会消除");
            return;
        }

        LogBoard($"牌面一致，准备消除: first={DescribeTile(_selectedTile.Data)}, second={DescribeTile(tileView.Data)}");
        RemoveMatchedPair(_selectedTile, tileView);
    }

    /// <summary>
    /// 移除一对成功匹配的牌，并刷新整桌状态。
    /// </summary>
    private async void RemoveMatchedPair(TileView a, TileView b)
    {
        var feedback = GetMatchFeedback();

        _interactionLocked = true;
        _selectedTile = null;
        _pressedTile = null;
        _draggingTile = null;

        var rectA = a.GetGlobalRect();
        var rectB = b.GetGlobalRect();
        var centerA = GetRectCenter(rectA);
        var centerB = GetRectCenter(rectB);
        var collisionCenter = (centerA + centerB) * 0.5f;
        var stageAxis = Vector2.Right;
        var stageGap = ResolveStageGap(rectA, rectB, stageAxis, feedback.StageGapFactor);
        var hoverOffset = new Vector2(0.0f, -feedback.StageLiftHeight);
        var stageCenterA = collisionCenter - stageAxis * stageGap;
        var stageCenterB = collisionCenter + stageAxis * stageGap;
        var stagePositionA = stageCenterA - rectA.Size * 0.5f + hoverOffset;
        var stagePositionB = stageCenterB - rectB.Size * 0.5f + hoverOffset;
        var crashPositionA = collisionCenter - rectA.Size * 0.5f;
        var crashPositionB = collisionCenter - rectB.Size * 0.5f;

        // 第一段：两张牌各自悬浮到碰撞前的左右侧或前后侧预备位。
        var stageTween = CreateTween();
        stageTween.SetParallel(true);
        stageTween.SetEase(Tween.EaseType.Out);
        stageTween.SetTrans(Tween.TransitionType.Cubic);
        stageTween.TweenProperty(a, "global_position", stagePositionA, feedback.StageMoveDuration);
        stageTween.TweenProperty(b, "global_position", stagePositionB, feedback.StageMoveDuration);
        stageTween.TweenProperty(a, "scale", Vector2.One * feedback.StageScale, feedback.StageMoveDuration);
        stageTween.TweenProperty(b, "scale", Vector2.One * feedback.StageScale, feedback.StageMoveDuration);
        await ToSignal(stageTween, Tween.SignalName.Finished);

        await ToSignal(GetTree().CreateTimer(feedback.StageHoldDuration), SceneTreeTimer.SignalName.Timeout);

        // 第二段：两张牌以可见速度飞向同一个中点，形成“碰撞”感。
        var crashTween = CreateTween();
        crashTween.SetParallel(true);
        crashTween.SetEase(Tween.EaseType.In);
        crashTween.SetTrans(Tween.TransitionType.Quart);
        crashTween.TweenProperty(a, "global_position", crashPositionA, feedback.CrashDuration);
        crashTween.TweenProperty(b, "global_position", crashPositionB, feedback.CrashDuration);
        crashTween.TweenProperty(a, "scale", Vector2.One * feedback.ImpactScale, feedback.CrashDuration);
        crashTween.TweenProperty(b, "scale", Vector2.One * feedback.ImpactScale, feedback.CrashDuration);
        await ToSignal(crashTween, Tween.SignalName.Finished);

        a.Modulate = feedback.ImpactTintColor;
        b.Modulate = feedback.ImpactTintColor;
        ShowImpactFlash(collisionCenter, feedback);
        ShowScorePopup(collisionCenter, feedback.MatchScore, feedback);

        a.Data.Removed = true;
        b.Data.Removed = true;
        _matchCount += 1;
        _score += feedback.MatchScore;

        // 第三段：碰撞之后两张牌一起收缩淡出。
        var fadeTween = CreateTween();
        fadeTween.SetParallel(true);
        fadeTween.TweenProperty(a, "modulate", new Color(1f, 1f, 1f, 0f), feedback.FadeDuration);
        fadeTween.TweenProperty(b, "modulate", new Color(1f, 1f, 1f, 0f), feedback.FadeDuration);
        fadeTween.TweenProperty(a, "scale", Vector2.One * 0.54f, feedback.FadeDuration);
        fadeTween.TweenProperty(b, "scale", Vector2.One * 0.54f, feedback.FadeDuration);
        await ToSignal(fadeTween, Tween.SignalName.Finished);

        a.Visible = false;
        b.Visible = false;
        a.Modulate = Colors.White;
        b.Modulate = Colors.White;
        a.Scale = Vector2.One;
        b.Scale = Vector2.One;

        RefreshTileStates();
        RefreshVisibleLayers();

        LogBoard($"完成消除: a={DescribeTile(a.Data)}, b={DescribeTile(b.Data)}, score={_score}, matches={_matchCount}");
        EmitSignal(SignalName.BoardStateChanged, $"成功消除一对 {a.Data.Type}，当前已消除 {_matchCount} 对");
        _interactionLocked = false;
    }

    /// <summary>清空当前点击选中状态。</summary>
    private void ClearSelection(bool refreshVisual)
    {
        if (_selectedTile is not null)
        {
            LogBoard($"清空当前选中: {DescribeTile(_selectedTile.Data)}, refresh_visual={refreshVisual}");
        }

        _selectedTile = null;
        if (refreshVisual)
        {
            RefreshTileStates();
        }
    }

    /// <summary>
    /// 重置本轮鼠标按下/拖拽相关的全部临时状态。
    /// </summary>
    private void ResetPointerState()
    {
        _pressedTile = null;
        _draggingTile = null;
        _pointerPressScreenPosition = Vector2.Zero;
        _dragTileStartPosition = Vector2.Zero;
        _dragPointerOffset = Vector2.Zero;
        _dragTileOriginalZIndex = 0;
    }

    /// <summary>返回当前所有尚未移除的逻辑牌。</summary>
    private IReadOnlyCollection<AppTileData> GetActiveTiles()
    {
        return _currentLayout?.Tiles
            .Where(tile => !tile.Removed)
            .ToList() ?? [];
    }

    /// <summary>
    /// 根据屏幕坐标选择当前最上层且可见的那张牌。
    /// </summary>
    private TileView? PickTopTileAtScreenPoint(Vector2 screenPoint)
    {
        var pickedTile = _tileViews
            .Where(tileView => tileView.Visible && !tileView.Data.Removed)
            .OrderByDescending(tileView => tileView.ZIndex)
            .FirstOrDefault(tileView => tileView.ContainsScreenPoint(screenPoint));

        if (pickedTile is not null)
        {
            LogBoard($"命中检测结果: {DescribeTile(pickedTile.Data)}");
        }

        return pickedTile;
    }

    /// <summary>计算牌桌在可用显示区域内的居中原点。</summary>
    private Vector2 ComputeCenteredOrigin(LevelLayout layout)
    {
        var viewportSize = GetViewportRect().Size;
        var bounds = GridMath.GetWorldBounds(layout.Tiles);
        var boardAreaPosition = new Vector2(SidePadding, TopReservedHeight);
        var boardAreaSize = new Vector2(
            Mathf.Max(0.0f, viewportSize.X - SidePadding * 2.0f),
            Mathf.Max(0.0f, viewportSize.Y - TopReservedHeight - BottomReservedHeight));

        return boardAreaPosition + (boardAreaSize - bounds.Size) * 0.5f - bounds.Position;
    }

    /// <summary>构造当前布局摘要，供调试面板和日志使用。</summary>
    private static string BuildSummary(LevelLayout layout, string sourceName, int visibleMaxLayer, int score, int matchCount, int movableCount)
    {
        var layerCounts = layout.Tiles
            .Where(tile => !tile.Removed)
            .GroupBy(tile => tile.GZ)
            .OrderBy(group => group.Key)
            .Select(group => $"L{group.Key}:{group.Count()}")
            .ToArray();

        return $"{sourceName} | 总牌数 {layout.Tiles.Count(tile => !tile.Removed)} | 可动 {movableCount} | 配对 {matchCount} | 分数 {score} | 显示 <= L{visibleMaxLayer} | 总层数 {layerCounts.Length} | {string.Join(" / ", layerCounts)}";
    }

    /// <summary>若场景未显式绑定档案目录，则从默认路径回退加载。</summary>
    private void EnsureProfileCatalogLoaded()
    {
        if (ProfileCatalog is not null)
        {
            return;
        }

        ProfileCatalog = GD.Load<LayoutProfileCatalog>(DefaultCatalogPath);
        if (ProfileCatalog is null)
        {
            GD.PushError($"[BoardController] 无法加载默认档案目录: {DefaultCatalogPath}");
        }
    }

    /// <summary>选择默认规则档案，若目录为空则退回最小空规则。</summary>
    /// <summary>
    /// 统一读取消除反馈资源。
    /// 如果场景没有绑定资源，则回退到默认路径；再失败时则生成一份运行时默认值，
    /// 保证表现资源缺失不会把核心配对流程直接打断。
    /// </summary>
    private MatchFeedbackConfig GetMatchFeedback()
    {
        if (MatchFeedbackConfig is not null)
        {
            return MatchFeedbackConfig;
        }

        MatchFeedbackConfig = GD.Load<MatchFeedbackConfig>(DefaultMatchFeedbackPath);
        if (MatchFeedbackConfig is null)
        {
            GD.PushWarning($"[BoardController] 无法加载默认消除反馈配置，使用运行时默认值: {DefaultMatchFeedbackPath}");
            MatchFeedbackConfig = new MatchFeedbackConfig();
        }

        return MatchFeedbackConfig;
    }

    /// <summary>
    /// 在 _Ready 阶段提前准备反馈资源。
    /// 这样如果 Inspector 里忘记绑定资源，会更早发现并回退，而不是等首次消除时才触发。
    /// </summary>
    private void EnsureMatchFeedbackLoaded()
    {
        _ = GetMatchFeedback();
    }

    private void EnsureDefaultProfileSelected()
    {
        var profiles = GetProfiles();
        if (profiles.Length == 0)
        {
            _layoutRules = new LayoutRules();
            _profileId = "missing";
            _profileDisplayName = "缺少档案";
            return;
        }

        var defaultProfileId = string.IsNullOrWhiteSpace(ProfileCatalog.DefaultProfileId)
            ? profiles[0].ProfileId
            : ProfileCatalog.DefaultProfileId;

        SetLayoutProfile(defaultProfileId);
    }

    /// <summary>按 id 查找规则档案。</summary>
    private LayoutProfileConfig? FindProfile(string profileId)
    {
        return GetProfiles()
            .FirstOrDefault(profile => profile.ProfileId == profileId);
    }

    /// <summary>统一输出棋盘相关日志。</summary>
    private static void LogBoard(string message)
    {
        GD.Print($"[BoardController] {message}");
    }

    /// <summary>构造适合日志输出的麻将摘要。</summary>
    private static string DescribeTile(AppTileData tile)
    {
        return $"Tile#{tile.Id} {tile.Type} @ ({tile.GX},{tile.GY},{tile.GZ})";
    }

    /// <summary>返回矩形中心点。</summary>
    private static Vector2 GetRectCenter(Rect2 rect)
    {
        return rect.Position + rect.Size * 0.5f;
    }

    /// <summary>
     /// 计算两张牌在碰撞前应保留的展开间距。
     /// </summary>
    private static float ResolveStageGap(Rect2 rectA, Rect2 rectB, Vector2 axis, float stageGapFactor)
    {
        if (Mathf.Abs(axis.X) > Mathf.Abs(axis.Y))
        {
            return (rectA.Size.X + rectB.Size.X) * 0.5f * stageGapFactor;
        }

        return (rectA.Size.Y + rectB.Size.Y) * 0.5f * stageGapFactor;
    }

    /// <summary>
    /// 在碰撞点附近生成一个“+分数”的轻量提示。
    /// </summary>
    /// <summary>
    /// 在碰撞中点生成一个短促白闪。
    /// </summary>
    private void ShowImpactFlash(Vector2 globalCenter, MatchFeedbackConfig feedback)
    {
        var localCenter = ToLocal(globalCenter);
        var flashSize = new Vector2(feedback.FlashSize, feedback.FlashSize);
        var radius = (int)(feedback.FlashSize * 0.5f);
        var flashStyle = new StyleBoxFlat
        {
            BgColor = feedback.FlashColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
        };

        var flash = new Panel
        {
            Position = localCenter - flashSize * 0.5f,
            Size = flashSize,
            ZIndex = SafeMaxDragZIndex,
            Scale = Vector2.One * 0.35f,
            Modulate = new Color(feedback.FlashColor.R, feedback.FlashColor.G, feedback.FlashColor.B, 0.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        flash.AddThemeStyleboxOverride("panel", flashStyle);
        flash.PivotOffset = flashSize * 0.5f;
        AddChild(flash);

        var flashTween = CreateTween();
        flashTween.SetParallel(true);
        flashTween.SetEase(Tween.EaseType.Out);
        flashTween.SetTrans(Tween.TransitionType.Cubic);
        flashTween.TweenProperty(
            flash,
            "modulate",
            new Color(feedback.FlashColor.R, feedback.FlashColor.G, feedback.FlashColor.B, feedback.FlashMaxAlpha),
            feedback.FlashDuration * 0.35).From(new Color(feedback.FlashColor.R, feedback.FlashColor.G, feedback.FlashColor.B, 0.0f));
        flashTween.TweenProperty(flash, "scale", Vector2.One * feedback.FlashExpandScale, feedback.FlashDuration);
        flashTween.Chain().TweenProperty(
            flash,
            "modulate",
            new Color(feedback.FlashColor.R, feedback.FlashColor.G, feedback.FlashColor.B, 0.0f),
            feedback.FlashDuration * 0.65);
        flashTween.Finished += flash.QueueFree;
    }

    /// <summary>
    /// 判断两个牌面主体矩形是否已经进入可接受的接触范围。
    /// </summary>
    /// <summary>
    /// 在碰撞点附近生成更强的分数提示，强调“已经成功消除”。
    /// </summary>
    private void ShowScorePopup(Vector2 globalCenter, int scoreValue, MatchFeedbackConfig feedback)
    {
        var localCenter = ToLocal(globalCenter);
        var popup = new Label
        {
            Text = $"+{scoreValue}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ZIndex = SafeMaxDragZIndex,
            Scale = Vector2.One * feedback.ScoreStartScale,
            Modulate = new Color(feedback.ScoreFontColor.R, feedback.ScoreFontColor.G, feedback.ScoreFontColor.B, 0.0f),
        };
        popup.AddThemeFontSizeOverride("font_size", feedback.ScoreFontSize);
        popup.AddThemeColorOverride("font_color", feedback.ScoreFontColor);
        popup.AddThemeColorOverride("font_outline_color", feedback.ScoreOutlineColor);
        popup.AddThemeConstantOverride("outline_size", feedback.ScoreOutlineSize);
        AddChild(popup);

        popup.Size = popup.GetMinimumSize() + new Vector2(18.0f, 10.0f);
        popup.PivotOffset = popup.Size * 0.5f;
        popup.Position = localCenter - popup.Size * 0.5f + new Vector2(0.0f, -8.0f);

        var popupTween = CreateTween();
        popupTween.SetParallel(true);
        popupTween.TweenProperty(
            popup,
            "position",
            popup.Position + new Vector2(0.0f, -feedback.ScoreFloatDistance),
            feedback.ScorePopupDuration);
        popupTween.TweenProperty(popup, "scale", Vector2.One * feedback.ScorePeakScale, feedback.ScoreBounceUpDuration)
            .From(Vector2.One * feedback.ScoreStartScale);
        popupTween.Chain().TweenProperty(popup, "scale", Vector2.One * feedback.ScoreSettleScale, feedback.ScoreBounceDownDuration);
        popupTween.TweenProperty(
            popup,
            "modulate",
            new Color(feedback.ScoreFontColor.R, feedback.ScoreFontColor.G, feedback.ScoreFontColor.B, 0.0f),
            feedback.ScorePopupDuration)
            .From(new Color(feedback.ScoreFontColor.R, feedback.ScoreFontColor.G, feedback.ScoreFontColor.B, feedback.ScoreMaxAlpha));
        popupTween.Finished += popup.QueueFree;
    }

    /// <summary>
    /// 判定两张牌是否已经进入可接受的接触容差范围。
    /// 这属于拖拽手感参数，因此走表现配置而不是业务规则配置。
    /// </summary>
    private bool IsWithinContactTolerance(Rect2 draggedRect, Rect2 targetRect)
    {
        return draggedRect.Grow(GetMatchFeedback().DragContactTolerance).Intersects(targetRect);
    }

    /// <summary>
    /// 计算两个矩形之间的最小间距，用于在多个候选目标中选出最近的那一个。
    /// </summary>
    private static float GetRectGapDistance(Rect2 a, Rect2 b)
    {
        var dx = 0.0f;
        if (a.End.X < b.Position.X)
        {
            dx = b.Position.X - a.End.X;
        }
        else if (b.End.X < a.Position.X)
        {
            dx = a.Position.X - b.End.X;
        }

        var dy = 0.0f;
        if (a.End.Y < b.Position.Y)
        {
            dy = b.Position.Y - a.End.Y;
        }
        else if (b.End.Y < a.Position.Y)
        {
            dy = a.Position.Y - b.End.Y;
        }

        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
