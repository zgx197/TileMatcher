using Godot;
using System.Collections.Generic;
using System.Linq;
using TileMatcher.Config;
using TileMatcher.Data;
using TileMatcher.Grid;
using TileMatcher.Layout;
using TileMatcher.Tile;

namespace TileMatcher.Board;

/// <summary>
/// 负责把布局数据转成可见棋盘节点的核心控制器。
/// </summary>
/// <remarks>
/// 它主要负责：
/// - 管理当前规则档案
/// - 生成或加载布局
/// - 在进入棋盘前统一校验布局
/// - 把 TileData 实例化成 TileView 并完成居中摆放
/// </remarks>
public partial class BoardController : Node2D
{
    // 当场景没有显式绑定档案目录时，退回这份默认资源。
    private const string DefaultCatalogPath = "res://configs/layout_profiles/default_catalog.tres";
    // 棋盘在屏幕两侧保留的安全边距。
    private const float SidePadding = 36.0f;
    // 顶部 UI 预留高度，避免棋盘压住状态栏。
    private const float TopReservedHeight = 132.0f;
    // 底部调试和留白预留高度。
    private const float BottomReservedHeight = 172.0f;
    // 不同层之间的 ZIndex 主步长，确保层级永远先于行列排序。
    private const int LayerZStride = 128;
    // 同一层内按行列细分 ZIndex，减少绘制顺序错误。
    private const int RowZStride = 8;

    // 当前棋盘上实际存在的 TileView 实例，用于集中刷新和销毁。
    private readonly List<TileView> _tileViews = [];
    // 当前真正参与生成和校验的运行时规则对象。
    private LayoutRules _layoutRules = new();
    // 当前规则档案的稳定 id。
    private string _profileId = string.Empty;
    // 当前规则档案的显示名称。
    private string _profileDisplayName = "未配置";
    // 当前已经应用到棋盘上的布局数据。
    private LevelLayout? _currentLayout;
    // 当前布局来源，主要用于调试摘要中区分“固定原型 / 随机布局”。
    private string _currentSourceName = "未加载";
    // 当前允许显示到哪一层，供调试滑杆控制。
    private int _visibleMaxLayer;

    [Export]
    public PackedScene TileScene { get; set; } = null!;

    /// <summary>
    /// 档案目录资源。
    /// 优先允许场景在 Inspector 中显式绑定，未绑定时再退回默认路径加载。
    /// </summary>
    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    [Signal]
    public delegate void BoardGeneratedEventHandler(string summary);

    /// <summary>
    /// 当前布局中的最高层号。
    /// </summary>
    /// <remarks>
    /// 这是“最大层号”，不等于实际层数。
    /// 例如当前有 L0/L2 时，它会返回 2，而不是 2 层。
    /// </remarks>
    public int MaxLayer => _currentLayout?.Tiles.Count > 0 ? _currentLayout.Tiles.Max(tile => tile.GZ) : 0;

    /// <summary>当前调试视图允许显示的最高层号。</summary>
    public int VisibleMaxLayer => _visibleMaxLayer;

    /// <summary>当前规则档案 id。</summary>
    public string CurrentProfileId => _profileId;

    /// <summary>当前规则档案显示名。</summary>
    public string CurrentProfileDisplayName => _profileDisplayName;

    /// <summary>当前运行时规则对象。</summary>
    public LayoutRules CurrentRules => _layoutRules;

    public override void _Ready()
    {
        if (TileScene is null)
        {
            TileScene = GD.Load<PackedScene>("res://scenes/tile/Tile.tscn");
        }

        EnsureProfileCatalogLoaded();
        EnsureDefaultProfileSelected();
    }

    /// <summary>加载固定原型布局。</summary>
    public void LoadPrototype()
    {
        ApplyLayout(PrototypeLayoutFactory.CreateSingleLevelPrototype(_layoutRules), "固定原型");
    }

    /// <summary>使用当前规则生成随机布局。</summary>
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

        // 运行时逻辑仍使用纯 C# 规则对象，便于保持校验与生成器的稳定性。
        _profileId = profile.ProfileId;
        _profileDisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.ProfileId : profile.DisplayName;
        _layoutRules = profile.ToRuntimeRules();
    }

    /// <summary>设置当前最大可见层，只影响显示不改动数据。</summary>
    public void SetVisibleMaxLayer(int visibleMaxLayer)
    {
        if (_currentLayout is null)
        {
            _visibleMaxLayer = 0;
            return;
        }

        _visibleMaxLayer = Mathf.Clamp(visibleMaxLayer, 0, MaxLayer);
        RefreshVisibleLayers();
    }

    public string GetCurrentSummary()
    {
        return _currentLayout is null
            ? "尚未加载布局"
            : BuildSummary(_currentLayout, _currentSourceName, _visibleMaxLayer);
    }

    public string GetCurrentRulesSummary()
    {
        return $"规则配置 | 档案 {CurrentProfileDisplayName} | 牌形 {CurrentRules.TileWidthUnits}x{CurrentRules.TileHeightUnits} | 上层偏移 {CurrentRules.GetOffsetModeDisplayName()} | 层数 {CurrentRules.MinLayerCount}-{CurrentRules.MaxLayerCount} | 底层至少 {CurrentRules.MinBottomLayerTileCount} | 完整覆盖 {(CurrentRules.RequireStrictSupport ? "开" : "关")} | 上层收缩 {(CurrentRules.RequireUpperLayerStrictlySmaller ? "开" : "关")}";
    }

    /// <summary>返回当前目录中的全部规则档案。</summary>
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
    /// 把一份布局应用到棋盘上。
    /// </summary>
    /// <remarks>
        /// 即使校验不通过，当前实现也会继续渲染，方便调试错误布局。
    /// 这意味着“看到棋盘”不代表“布局已经合法”，调试时仍需结合日志与摘要判断。
    /// </remarks>
    private void ApplyLayout(LevelLayout layout, string sourceName)
    {
        _currentLayout = layout;
        _currentSourceName = sourceName;

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
        GD.Print($"[BoardController] 应用布局: {sourceName}, 牌数={layout.Tiles.Count}, 层数={layout.Tiles.Select(tile => tile.GZ).Distinct().Count()}");

        var drawOrder = layout.Tiles
            .OrderBy(tile => tile.GZ)
            .ThenBy(tile => tile.GY)
            .ThenBy(tile => tile.GX)
            .ToList();

        foreach (var tileData in drawOrder)
        {
            var tile = TileScene.Instantiate<TileView>();
            tile.ApplyData(tileData);
            tile.Position = GridMath.GridToWorld(tileData.GX, tileData.GY, tileData.GZ, boardOrigin);
            tile.ZIndex = tileData.GZ * LayerZStride + tileData.GY * RowZStride + tileData.GX;

            AddChild(tile);
            _tileViews.Add(tile);
        }

        _visibleMaxLayer = MaxLayer;
        RefreshVisibleLayers();

        EmitSignal(SignalName.BoardGenerated, BuildSummary(layout, sourceName, _visibleMaxLayer));
    }

    /// <summary>根据当前层过滤设置刷新牌可见性。</summary>
    private void RefreshVisibleLayers()
    {
        foreach (var tileView in _tileViews)
        {
            tileView.Visible = tileView.Data.GZ <= _visibleMaxLayer;
        }
    }

    /// <summary>计算棋盘整体居中时的原点偏移。</summary>
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

    /// <summary>构造当前布局摘要文本。</summary>
    private static string BuildSummary(LevelLayout layout, string sourceName, int visibleMaxLayer)
    {
        var layerCounts = layout.Tiles
            .GroupBy(tile => tile.GZ)
            .OrderBy(group => group.Key)
            .Select(group => $"L{group.Key}:{group.Count()}")
            .ToArray();

        return $"{sourceName} | 总牌数 {layout.Tiles.Count} | 显示 <= L{visibleMaxLayer} | 总层数 {layerCounts.Length} | {string.Join(" / ", layerCounts)}";
    }

    /// <summary>在未显式绑定时，从默认路径加载档案目录。</summary>
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

    /// <summary>选择默认档案，若目录为空则退回基础空规则。</summary>
    private void EnsureDefaultProfileSelected()
    {
        var profiles = GetProfiles();
        if (profiles.Length == 0)
        {
            // 用一个最小可用的空规则对象兜底，避免后续所有入口都因 null 崩溃。
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

    /// <summary>按 id 查找档案。</summary>
    private LayoutProfileConfig? FindProfile(string profileId)
    {
        return GetProfiles()
            .FirstOrDefault(profile => profile.ProfileId == profileId);
    }
}
