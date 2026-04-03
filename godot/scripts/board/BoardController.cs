using Godot;
using System.Collections.Generic;
using System.Linq;
using TileMatcher.Data;
using TileMatcher.Grid;
using TileMatcher.Layout;
using TileMatcher.Tile;

namespace TileMatcher.Board;

public partial class BoardController : Node2D
{
    private const float SidePadding = 36.0f;
    private const float TopReservedHeight = 132.0f;
    private const float BottomReservedHeight = 172.0f;
    private const int LayerZStride = 128;
    private const int RowZStride = 8;

    private readonly List<TileView> _tileViews = [];
    private readonly LayoutRules _layoutRules = GameLayoutProfiles.VitaMahjongSingleLevel;
    private LevelLayout? _currentLayout;
    private string _currentSourceName = "未加载";
    private int _visibleMaxLayer;

    [Export]
    public PackedScene TileScene { get; set; } = null!;

    [Signal]
    public delegate void BoardGeneratedEventHandler(string summary);

    public int MaxLayer => _currentLayout?.Tiles.Count > 0 ? _currentLayout.Tiles.Max(tile => tile.GZ) : 0;

    public int VisibleMaxLayer => _visibleMaxLayer;

    public override void _Ready()
    {
        if (TileScene is null)
        {
            TileScene = GD.Load<PackedScene>("res://scenes/tile/Tile.tscn");
        }
    }

    public void LoadPrototype()
    {
        ApplyLayout(PrototypeLayoutFactory.CreateSingleLevelPrototype(), "固定原型");
    }

    public void GenerateRandomBoard()
    {
        ApplyLayout(RandomStackLayoutGenerator.Generate(1, _layoutRules), "随机布局");
    }

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

    private void RefreshVisibleLayers()
    {
        foreach (var tileView in _tileViews)
        {
            tileView.Visible = tileView.Data.GZ <= _visibleMaxLayer;
        }
    }

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

    private static string BuildSummary(LevelLayout layout, string sourceName, int visibleMaxLayer)
    {
        var layerCounts = layout.Tiles
            .GroupBy(tile => tile.GZ)
            .OrderBy(group => group.Key)
            .Select(group => $"L{group.Key}:{group.Count()}")
            .ToArray();

        return $"{sourceName} | 总牌数 {layout.Tiles.Count} | 显示 <= L{visibleMaxLayer} | 总层数 {layerCounts.Length} | {string.Join(" / ", layerCounts)}";
    }
}
