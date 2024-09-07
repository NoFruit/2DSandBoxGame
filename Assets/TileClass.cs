using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "newtileclass", menuName = "Tile Class")]
public class TileClass : ScriptableObject
{
    public string tileName;
    public TileClass wallVariant;
    // public Sprite tileSprite;
    public Sprite[] tileSprites;
    public bool isBackground = false;
    public Sprite tileDrop;
    public bool naturallyPlaced = false;

    // 创建实例静态类
    public static TileClass CreateInstance(TileClass tile, bool isNaturallyPlaced)
    {
        TileClass newInstance = ScriptableObject.CreateInstance<TileClass>();
        newInstance.Init(tile, isNaturallyPlaced);
        return newInstance;
    }


    public void Init(TileClass tile, bool isNaturallyPlaced)
    {
        tileName = tile.tileName;
        wallVariant = tile.wallVariant;
        tileSprites = tile.tileSprites;
        isBackground = tile.isBackground;
        tileDrop = tile.tileDrop;
        naturallyPlaced = isNaturallyPlaced;
    }

/*
    public TileClass(TileClass tile, bool isNaturallyPlaced)
    {
        tileName = tile.tileName;
        wallVariant = tile.wallVariant;
        tileSprites = tile.tileSprites;
        isBackground = tile.isBackground;
        tileDrop = tile.tileDrop;
        naturallyPlaced = isNaturallyPlaced;
    }
*/
}
