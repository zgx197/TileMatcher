using Godot;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Tile;

public partial class TileView : Node2D
{
    private Label _label = null!;
    private StyleBoxFlat _bodyStyle = null!;
    private StyleBoxFlat _depthStyle = null!;
    private StyleBoxFlat _shadowStyle = null!;

    public AppTileData Data { get; private set; } = null!;

    public override void _Ready()
    {
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
    }

    public void ApplyData(AppTileData tileData)
    {
        Data = tileData;
        Name = $"Tile_{tileData.Id}_{tileData.Type}";

        _label.Text = tileData.Type;
        _label.Position = new Vector2(0.0f, 18.0f);
        _label.Size = GridConfig.TileSize;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 36);
        _label.AddThemeColorOverride("font_color", ResolveTextColor(tileData.Type));

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawStyleBox(_shadowStyle, new Rect2(9.0f, 10.0f, GridConfig.TileWidth, GridConfig.TileHeight));
        DrawStyleBox(_depthStyle, new Rect2(4.0f, 6.0f, GridConfig.TileWidth, GridConfig.TileHeight));
        DrawStyleBox(_bodyStyle, new Rect2(0.0f, 0.0f, GridConfig.TileWidth, GridConfig.TileHeight));
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
