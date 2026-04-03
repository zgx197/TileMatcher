using Godot;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Tile;

public partial class TileView : Node2D
{
    private static readonly Color[] BodyPalette =
    [
        new(0.97f, 0.97f, 0.93f, 1.0f),
        new(0.97f, 0.89f, 0.75f, 1.0f),
        new(0.80f, 0.91f, 0.98f, 1.0f),
        new(0.86f, 0.96f, 0.82f, 1.0f),
        new(0.93f, 0.84f, 0.97f, 1.0f),
        new(0.99f, 0.86f, 0.88f, 1.0f),
    ];

    private static readonly Color[] DepthPalette =
    [
        new(0.80f, 0.82f, 0.88f, 1.0f),
        new(0.88f, 0.70f, 0.52f, 1.0f),
        new(0.53f, 0.72f, 0.86f, 1.0f),
        new(0.56f, 0.76f, 0.53f, 1.0f),
        new(0.70f, 0.58f, 0.82f, 1.0f),
        new(0.86f, 0.60f, 0.67f, 1.0f),
    ];

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

    public AppTileData Data { get; private set; } = null!;

    public override void _Ready()
    {
        EnsureInitialized();
    }

    public void ApplyData(AppTileData tileData)
    {
        EnsureInitialized();

        Data = tileData;
        Name = $"Tile_{tileData.Id}_{tileData.Type}";

        _label.Text = tileData.Type;
        _label.Position = new Vector2(0.0f, 18.0f);
        _label.Size = GridConfig.TileSize;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 36);
        _label.AddThemeColorOverride("font_color", ResolveTextColor(tileData.Type));

        ApplyLayerDebugStyle(tileData.GZ);
        QueueRedraw();
    }

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
        DrawStyleBox(_shadowStyle, new Rect2(9.0f, 10.0f, GridConfig.TileWidth, GridConfig.TileHeight));
        DrawStyleBox(_depthStyle, new Rect2(4.0f, 6.0f, GridConfig.TileWidth, GridConfig.TileHeight));
        DrawStyleBox(_bodyStyle, new Rect2(0.0f, 0.0f, GridConfig.TileWidth, GridConfig.TileHeight));
    }

    private void ApplyLayerDebugStyle(int layer)
    {
        var paletteIndex = Mathf.PosMod(layer, BodyPalette.Length);

        _bodyStyle.BgColor = BodyPalette[paletteIndex];
        _bodyStyle.BorderColor = BorderPalette[paletteIndex];
        _depthStyle.BgColor = DepthPalette[paletteIndex];
    }

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
}
