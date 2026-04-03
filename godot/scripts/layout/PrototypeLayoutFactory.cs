using TileMatcher.Data;
using GridVector2I = Godot.Vector2I;

namespace TileMatcher.Layout;

/// <summary>
/// 提供一份固定原型布局，用于快速人工观察堆叠关系。
/// </summary>
/// <remarks>
/// 这份原型的目标不是生成真实关卡，而是稳定复现当前规则下的层间偏移效果。
/// 它必须与当前 LayoutRules 保持一致，否则调试会出现两套标准。
/// </remarks>
public static class PrototypeLayoutFactory
{
    /// <summary>创建固定原型布局。</summary>
    public static LevelLayout CreateSingleLevelPrototype(LayoutRules? rules = null, int levelId = 1)
    {
        rules ??= new LayoutRules();
        var tileShape = rules.CreateTileShape();
        var width = tileShape.WidthUnits;
        var height = tileShape.HeightUnits;
        var origin1 = GetPrototypeLayerOrigin(1, rules, tileShape);
        var origin2 = GetPrototypeLayerOrigin(2, rules, tileShape);
        var origin3 = GetPrototypeLayerOrigin(3, rules, tileShape);
        var layout = new LevelLayout { LevelId = levelId };

        layout.Tiles.AddRange(
        [
            new TileData { Id = 1, Type = "1B", GX = 0, GY = 0, GZ = 0 },
            new TileData { Id = 2, Type = "3B", GX = width, GY = 0, GZ = 0 },
            new TileData { Id = 3, Type = "5W", GX = width * 2, GY = 0, GZ = 0 },
            new TileData { Id = 4, Type = "7T", GX = width * 3, GY = 0, GZ = 0 },
            new TileData { Id = 5, Type = "2D", GX = 0, GY = height, GZ = 0 },
            new TileData { Id = 6, Type = "4D", GX = width, GY = height, GZ = 0 },
            new TileData { Id = 7, Type = "6D", GX = width * 2, GY = height, GZ = 0 },
            new TileData { Id = 8, Type = "8D", GX = width * 3, GY = height, GZ = 0 },
            new TileData { Id = 9, Type = "E", GX = 0, GY = height * 2, GZ = 0 },
            new TileData { Id = 10, Type = "S", GX = width, GY = height * 2, GZ = 0 },
            new TileData { Id = 11, Type = "W", GX = width * 2, GY = height * 2, GZ = 0 },
            new TileData { Id = 12, Type = "N", GX = width * 3, GY = height * 2, GZ = 0 },
            new TileData { Id = 13, Type = "1D", GX = 0, GY = height * 3, GZ = 0 },
            new TileData { Id = 14, Type = "3D", GX = width, GY = height * 3, GZ = 0 },
            new TileData { Id = 15, Type = "5D", GX = width * 2, GY = height * 3, GZ = 0 },
            new TileData { Id = 16, Type = "7D", GX = width * 3, GY = height * 3, GZ = 0 },
            new TileData { Id = 17, Type = "P", GX = origin1.X, GY = origin1.Y, GZ = 1 },
            new TileData { Id = 18, Type = "C", GX = origin1.X + width, GY = origin1.Y, GZ = 1 },
            new TileData { Id = 19, Type = "F", GX = origin1.X + width * 2, GY = origin1.Y, GZ = 1 },
            new TileData { Id = 20, Type = "G", GX = origin1.X, GY = origin1.Y + height, GZ = 1 },
            new TileData { Id = 21, Type = "R", GX = origin1.X + width, GY = origin1.Y + height, GZ = 1 },
            new TileData { Id = 22, Type = "2W", GX = origin1.X + width * 2, GY = origin1.Y + height, GZ = 1 },
            new TileData { Id = 23, Type = "4W", GX = origin1.X, GY = origin1.Y + height * 2, GZ = 1 },
            new TileData { Id = 24, Type = "6W", GX = origin1.X + width, GY = origin1.Y + height * 2, GZ = 1 },
            new TileData { Id = 25, Type = "8W", GX = origin1.X + width * 2, GY = origin1.Y + height * 2, GZ = 1 },
            new TileData { Id = 26, Type = "9W", GX = origin2.X, GY = origin2.Y, GZ = 2 },
            new TileData { Id = 27, Type = "E", GX = origin2.X + width, GY = origin2.Y, GZ = 2 },
            new TileData { Id = 28, Type = "S", GX = origin2.X, GY = origin2.Y + height, GZ = 2 },
            new TileData { Id = 29, Type = "W", GX = origin2.X + width, GY = origin2.Y + height, GZ = 2 },
            new TileData { Id = 30, Type = "N", GX = origin3.X, GY = origin3.Y, GZ = 3 },
        ]);

        return layout;
    }

    /// <summary>
    /// 计算某一层在原型布局中的累计原点。
    /// </summary>
    /// <remarks>
    /// 这里使用逐层累积偏移，而不是每层相对世界原点的绝对偏移。
    /// 这样可以保证高层一定建立在下层之上。
    /// </remarks>
    private static GridVector2I GetPrototypeLayerOrigin(int layer, LayoutRules rules, TileShape tileShape)
    {
        var origin = GridVector2I.Zero;

        for (var currentLayer = 1; currentLayer <= layer; currentLayer++)
        {
            origin += GetPrototypeLayerStepOffset(currentLayer, rules, tileShape);
        }

        return origin;
    }

    /// <summary>计算单次升层时应追加的偏移量。</summary>
    private static GridVector2I GetPrototypeLayerStepOffset(int layer, LayoutRules rules, TileShape tileShape)
    {
        var halfX = tileShape.WidthUnits / 2;
        var halfY = tileShape.HeightUnits / 2;

        return rules.UpperLayerOffsetMode switch
        {
            LayerOffsetMode.HalfX => new GridVector2I(halfX, 0),
            LayerOffsetMode.HalfY => new GridVector2I(0, halfY),
            LayerOffsetMode.HalfXY => new GridVector2I(halfX, halfY),
            _ => GridVector2I.Zero,
        };
    }
}
