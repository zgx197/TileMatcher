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
        var pairDeck = TileTypeDeckBuilder.BuildPrototypePairDeck(30);

        layout.Tiles.AddRange(
        [
            new TileData { Id = 1, Type = pairDeck[0], GX = 0, GY = 0, GZ = 0 },
            new TileData { Id = 2, Type = pairDeck[1], GX = width, GY = 0, GZ = 0 },
            new TileData { Id = 3, Type = pairDeck[2], GX = width * 2, GY = 0, GZ = 0 },
            new TileData { Id = 4, Type = pairDeck[3], GX = width * 3, GY = 0, GZ = 0 },
            new TileData { Id = 5, Type = pairDeck[4], GX = 0, GY = height, GZ = 0 },
            new TileData { Id = 6, Type = pairDeck[5], GX = width, GY = height, GZ = 0 },
            new TileData { Id = 7, Type = pairDeck[6], GX = width * 2, GY = height, GZ = 0 },
            new TileData { Id = 8, Type = pairDeck[7], GX = width * 3, GY = height, GZ = 0 },
            new TileData { Id = 9, Type = pairDeck[8], GX = 0, GY = height * 2, GZ = 0 },
            new TileData { Id = 10, Type = pairDeck[9], GX = width, GY = height * 2, GZ = 0 },
            new TileData { Id = 11, Type = pairDeck[10], GX = width * 2, GY = height * 2, GZ = 0 },
            new TileData { Id = 12, Type = pairDeck[11], GX = width * 3, GY = height * 2, GZ = 0 },
            new TileData { Id = 13, Type = pairDeck[12], GX = 0, GY = height * 3, GZ = 0 },
            new TileData { Id = 14, Type = pairDeck[13], GX = width, GY = height * 3, GZ = 0 },
            new TileData { Id = 15, Type = pairDeck[14], GX = width * 2, GY = height * 3, GZ = 0 },
            new TileData { Id = 16, Type = pairDeck[15], GX = width * 3, GY = height * 3, GZ = 0 },
            new TileData { Id = 17, Type = pairDeck[16], GX = origin1.X, GY = origin1.Y, GZ = 1 },
            new TileData { Id = 18, Type = pairDeck[17], GX = origin1.X + width, GY = origin1.Y, GZ = 1 },
            new TileData { Id = 19, Type = pairDeck[18], GX = origin1.X + width * 2, GY = origin1.Y, GZ = 1 },
            new TileData { Id = 20, Type = pairDeck[19], GX = origin1.X, GY = origin1.Y + height, GZ = 1 },
            new TileData { Id = 21, Type = pairDeck[20], GX = origin1.X + width, GY = origin1.Y + height, GZ = 1 },
            new TileData { Id = 22, Type = pairDeck[21], GX = origin1.X + width * 2, GY = origin1.Y + height, GZ = 1 },
            new TileData { Id = 23, Type = pairDeck[22], GX = origin1.X, GY = origin1.Y + height * 2, GZ = 1 },
            new TileData { Id = 24, Type = pairDeck[23], GX = origin1.X + width, GY = origin1.Y + height * 2, GZ = 1 },
            new TileData { Id = 25, Type = pairDeck[24], GX = origin1.X + width * 2, GY = origin1.Y + height * 2, GZ = 1 },
            new TileData { Id = 26, Type = pairDeck[25], GX = origin2.X, GY = origin2.Y, GZ = 2 },
            new TileData { Id = 27, Type = pairDeck[26], GX = origin2.X + width, GY = origin2.Y, GZ = 2 },
            new TileData { Id = 28, Type = pairDeck[27], GX = origin2.X, GY = origin2.Y + height, GZ = 2 },
            new TileData { Id = 29, Type = pairDeck[28], GX = origin2.X + width, GY = origin2.Y + height, GZ = 2 },
            new TileData { Id = 30, Type = pairDeck[29], GX = origin3.X, GY = origin3.Y, GZ = 3 },
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
