using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "newtileclass", menuName = "Tile Class")]
public class TileClass : ScriptableObject
{
    public string tileName;
    public bool isBackground = false;
    public bool isPassLight = false;
    public bool naturallyPlaced = false;
    public TileClass wallVariant;
    public Sprite tileDrop;
    public Sprite[] tileSprites;

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

}
