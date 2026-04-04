using Godot;
using TileMatcher.Board;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Tile;

/// <summary>
/// 单张麻将的可视化节点。
/// </summary>
/// <remarks>
/// 当前它仍然是一个偏渲染层的组件：
/// - 负责把逻辑数据画出来
/// - 负责把“可动 / 不可动 / 选中”这些运行态翻译成视觉反馈
/// - 不直接决定玩法规则
/// 这样后续无论是点击、拖拽还是自动测试，都可以复用同一套视觉状态更新。
/// </remarks>
public partial class TileView : Node2D
{
    /// <summary>不同层牌面主体的调试色板。</summary>
    private static readonly Color[] BodyPalette =
    [
        new(0.97f, 0.97f, 0.93f, 1.0f),
        new(0.97f, 0.89f, 0.75f, 1.0f),
        new(0.80f, 0.91f, 0.98f, 1.0f),
        new(0.86f, 0.96f, 0.82f, 1.0f),
        new(0.93f, 0.84f, 0.97f, 1.0f),
        new(0.99f, 0.86f, 0.88f, 1.0f),
    ];

    /// <summary>不同层侧边厚度区域的调试色板。</summary>
    private static readonly Color[] DepthPalette =
    [
        new(0.80f, 0.82f, 0.88f, 1.0f),
        new(0.88f, 0.70f, 0.52f, 1.0f),
        new(0.53f, 0.72f, 0.86f, 1.0f),
        new(0.56f, 0.76f, 0.53f, 1.0f),
        new(0.70f, 0.58f, 0.82f, 1.0f),
        new(0.86f, 0.60f, 0.67f, 1.0f),
    ];

    /// <summary>不同层边框颜色的调试色板。</summary>
    private static readonly Color[] BorderPalette =
    [
        new(0.71f, 0.74f, 0.82f, 1.0f),
        new(0.83f, 0.58f, 0.34f, 1.0f),
        new(0.35f, 0.55f, 0.74f, 1.0f),
        new(0.37f, 0.61f, 0.33f, 1.0f),
        new(0.56f, 0.43f, 0.73f, 1.0f),
        new(0.78f, 0.44f, 0.53f, 1.0f),
    ];

    private Label _label = null!;
    private StyleBoxFlat _bodyStyle = null!;
    private StyleBoxFlat _depthStyle = null!;
    private StyleBoxFlat _shadowStyle = null!;
    private bool _initialized;
    private bool _isMovable = true;
    private bool _isSelected;
    private float _feedbackOverlayAlpha;
    private float _feedbackEdgeAlpha;
    private Color _feedbackColor = Colors.Transparent;
    private bool _flashLeftEdge;
    private bool _flashRightEdge;
    private bool _flashTopEdge;
    private bool _flashBottomEdge;
    private Tween? _blockedFeedbackTween;

    /// <summary>当前视图绑定的逻辑数据。</summary>
    public AppTileData Data { get; private set; } = null!;

    public override void _Ready()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 把逻辑层 TileData 绑定到当前牌视图。
    /// </summary>
    public void ApplyData(AppTileData tileData)
    {
        EnsureInitialized();

        Data = tileData;
        var tileSize = GridMath.GetWorldSize(tileData);
        Name = $"Tile_{tileData.Id}_{tileData.Type}";

        _label.Text = tileData.Type;
        _label.Position = new Vector2(0.0f, 18.0f);
        _label.Size = tileSize;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 36);

        RefreshVisualState();
        QueueRedraw();
    }

    /// <summary>
    /// 更新当前牌的交互态展示。
    /// </summary>
    public void SetInteractionState(bool isMovable, bool isSelected)
    {
        EnsureInitialized();
        _isMovable = isMovable;
        _isSelected = isSelected;
        RefreshVisualState();
        QueueRedraw();
    }

    /// <summary>
    /// 播放一次“牌被锁住”的失败反馈。
    /// </summary>
    /// <remarks>
    /// 当前不使用锁图标，而是使用三种低成本但信息明确的视觉元素：
    /// 1. 牌本体轻微回弹
    /// 2. 半透明面闪
    /// 3. 对应方向的边缘颜色闪烁
    /// </remarks>
    public void PlayBlockedFeedback(TileBlockReason blockReason)
    {
        EnsureInitialized();

        _blockedFeedbackTween?.Kill();
        Scale = Vector2.One;
        ConfigureBlockedFeedbackEdges(blockReason);
        _feedbackColor = ResolveBlockedFeedbackColor(blockReason);
        _feedbackOverlayAlpha = 0.0f;
        _feedbackEdgeAlpha = 0.0f;
        QueueRedraw();

        var tween = CreateTween();
        _blockedFeedbackTween = tween;
        tween.SetParallel(true);
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenMethod(Callable.From<float>(SetFeedbackOverlayAlpha), 0.0f, 0.22f, 0.10);
        tween.TweenMethod(Callable.From<float>(SetFeedbackEdgeAlpha), 0.0f, 1.0f, 0.12);
        tween.TweenProperty(this, "scale", Vector2.One * 1.04f, 0.10).From(Vector2.One);
        tween.Chain().TweenProperty(this, "scale", Vector2.One, 0.14);
        tween.Chain().TweenMethod(Callable.From<float>(SetFeedbackOverlayAlpha), 0.22f, 0.0f, 0.18);
        tween.TweenMethod(Callable.From<float>(SetFeedbackEdgeAlpha), 1.0f, 0.0f, 0.18);
        tween.Finished += () =>
        {
            _feedbackOverlayAlpha = 0.0f;
            _feedbackEdgeAlpha = 0.0f;
            ClearBlockedFeedbackEdges();
            Scale = Vector2.One;
            QueueRedraw();
            _blockedFeedbackTween = null;
        };
    }

    /// <summary>
    /// 判断屏幕坐标是否命中当前牌主体区域。
    /// </summary>
    /// <remarks>
    /// 当前点击检测以牌面的主矩形为准，不把阴影和厚度区域算作命中范围。
    /// 这样用户点击的预期会更稳定，也更接近后续拖拽起手区域。
    /// </remarks>
    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        EnsureInitialized();

        var tileSize = Data is null ? GridConfig.TileSize : GridMath.GetWorldSize(Data);
        var origin = GetGlobalTransformWithCanvas().Origin;
        return new Rect2(origin, tileSize).HasPoint(screenPoint);
    }

    /// <summary>
    /// 返回当前牌面主体在全局空间中的矩形范围。
    /// </summary>
    /// <remarks>
    /// 拖拽结束时，使用这个矩形和其他牌做相交检测，判断是否发生“触碰”。
    /// 这里同样只使用牌面主体，不把阴影和厚度区域计算进去。
    /// </remarks>
    public Rect2 GetGlobalRect()
    {
        EnsureInitialized();

        var tileSize = Data is null ? GridConfig.TileSize : GridMath.GetWorldSize(Data);
        var origin = GetGlobalTransformWithCanvas().Origin;
        return new Rect2(origin, tileSize);
    }

    /// <summary>
    /// 延迟初始化内部节点和样式对象。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _label = GetNode<Label>("Label");

        _shadowStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.06f, 0.22f, 0.35f),
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusBottomLeft = 18,
        };

        _depthStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.65f, 0.67f, 0.88f, 1.0f),
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusBottomLeft = 18,
        };

        _bodyStyle = new StyleBoxFlat
        {
            BgColor = Colors.White,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusBottomLeft = 18,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.76f, 0.77f, 0.9f, 1.0f),
        };

        _initialized = true;
    }

    public override void _Draw()
    {
        // 数据尚未绑定时退回标准牌尺寸，避免编辑器或初始化阶段绘制崩溃。
        var tileSize = Data is null ? GridConfig.TileSize : GridMath.GetWorldSize(Data);
        DrawStyleBox(_shadowStyle, new Rect2(9.0f, 10.0f, tileSize.X, tileSize.Y));
        DrawStyleBox(_depthStyle, new Rect2(4.0f, 6.0f, tileSize.X, tileSize.Y));
        DrawStyleBox(_bodyStyle, new Rect2(0.0f, 0.0f, tileSize.X, tileSize.Y));

        if (_feedbackOverlayAlpha > 0.0f)
        {
            var overlayColor = new Color(_feedbackColor.R, _feedbackColor.G, _feedbackColor.B, _feedbackOverlayAlpha);
            DrawRect(new Rect2(0.0f, 0.0f, tileSize.X, tileSize.Y), overlayColor, true);
        }

        if (_feedbackEdgeAlpha > 0.0f)
        {
            var edgeColor = new Color(_feedbackColor.R, _feedbackColor.G, _feedbackColor.B, _feedbackEdgeAlpha);
            const float edgeThickness = 8.0f;

            if (_flashLeftEdge)
            {
                DrawRect(new Rect2(0.0f, 0.0f, edgeThickness, tileSize.Y), edgeColor, true);
            }

            if (_flashRightEdge)
            {
                DrawRect(new Rect2(tileSize.X - edgeThickness, 0.0f, edgeThickness, tileSize.Y), edgeColor, true);
            }

            if (_flashTopEdge)
            {
                DrawRect(new Rect2(0.0f, 0.0f, tileSize.X, edgeThickness), edgeColor, true);
            }

            if (_flashBottomEdge)
            {
                DrawRect(new Rect2(0.0f, tileSize.Y - edgeThickness, tileSize.X, edgeThickness), edgeColor, true);
            }
        }
    }

    /// <summary>
    /// 根据层级应用调试配色。
    /// </summary>
    private void ApplyLayerDebugStyle(int layer)
    {
        var paletteIndex = Mathf.PosMod(layer, BodyPalette.Length);

        _bodyStyle.BgColor = BodyPalette[paletteIndex];
        _bodyStyle.BorderColor = BorderPalette[paletteIndex];
        _depthStyle.BgColor = DepthPalette[paletteIndex];
    }

    /// <summary>
    /// 把逻辑状态翻译成牌面视觉反馈。
    /// </summary>
    private void RefreshVisualState()
    {
        var layer = Data?.GZ ?? 0;
        ApplyLayerDebugStyle(layer);

        var baseTextColor = Data is null ? Colors.Black : ResolveTextColor(Data.Type);
        _label.AddThemeColorOverride("font_color", baseTextColor);
        Modulate = Colors.White;

        _bodyStyle.BorderWidthLeft = 2;
        _bodyStyle.BorderWidthTop = 2;
        _bodyStyle.BorderWidthRight = 2;
        _bodyStyle.BorderWidthBottom = 2;
        _shadowStyle.BgColor = new Color(0.11f, 0.06f, 0.22f, 0.35f);

        if (_isSelected)
        {
            _bodyStyle.BorderWidthLeft = 5;
            _bodyStyle.BorderWidthTop = 5;
            _bodyStyle.BorderWidthRight = 5;
            _bodyStyle.BorderWidthBottom = 5;
            _bodyStyle.BorderColor = new Color(0.97f, 0.83f, 0.22f, 1.0f);
            _shadowStyle.BgColor = new Color(0.95f, 0.76f, 0.16f, 0.42f);
            return;
        }

        if (_isMovable)
        {
            return;
        }

        _bodyStyle.BorderColor = new Color(0.34f, 0.40f, 0.44f, 1.0f);
        _bodyStyle.BgColor = _bodyStyle.BgColor.Darkened(0.12f);
        _depthStyle.BgColor = _depthStyle.BgColor.Darkened(0.35f);
        _label.AddThemeColorOverride("font_color", new Color(baseTextColor.R, baseTextColor.G, baseTextColor.B, 0.78f));
        Modulate = new Color(0.78f, 0.78f, 0.80f, 0.72f);
    }

    /// <summary>
    /// 根据牌面类型选择文字颜色。
    /// </summary>
    private static Color ResolveTextColor(string type)
    {
        if (type.Contains('D') || type is "C" or "R")
        {
            return new Color(0.79f, 0.17f, 0.16f, 1.0f);
        }

        if (type.Contains('B') || type is "F" or "G")
        {
            return new Color(0.16f, 0.53f, 0.39f, 1.0f);
        }

        return new Color(0.15f, 0.22f, 0.55f, 1.0f);
    }

    /// <summary>设置失败反馈的面闪透明度。</summary>
    private void SetFeedbackOverlayAlpha(float alpha)
    {
        _feedbackOverlayAlpha = alpha;
        QueueRedraw();
    }

    /// <summary>设置失败反馈的边缘闪烁透明度。</summary>
    private void SetFeedbackEdgeAlpha(float alpha)
    {
        _feedbackEdgeAlpha = alpha;
        QueueRedraw();
    }

    /// <summary>根据锁定方向配置边缘高亮。</summary>
    private void ConfigureBlockedFeedbackEdges(TileBlockReason blockReason)
    {
        ClearBlockedFeedbackEdges();
        switch (blockReason)
        {
            case TileBlockReason.Above:
                _flashTopEdge = true;
                break;

            case TileBlockReason.LeftRight:
                _flashLeftEdge = true;
                _flashRightEdge = true;
                break;

            case TileBlockReason.TopBottom:
                _flashTopEdge = true;
                _flashBottomEdge = true;
                break;
        }
    }

    /// <summary>清空边缘反馈标记。</summary>
    private void ClearBlockedFeedbackEdges()
    {
        _flashLeftEdge = false;
        _flashRightEdge = false;
        _flashTopEdge = false;
        _flashBottomEdge = false;
    }

    /// <summary>不同锁定方向使用不同颜色，帮助玩家快速理解“为什么不行”。</summary>
    private static Color ResolveBlockedFeedbackColor(TileBlockReason blockReason)
    {
        return blockReason switch
        {
            TileBlockReason.Above => new Color(1.0f, 0.56f, 0.24f, 1.0f),
            TileBlockReason.LeftRight => new Color(0.98f, 0.82f, 0.18f, 1.0f),
            TileBlockReason.TopBottom => new Color(0.36f, 0.82f, 0.98f, 1.0f),
            _ => new Color(0.95f, 0.72f, 0.26f, 1.0f),
        };
    }
}
