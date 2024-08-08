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
    public bool tileDrop = true;
    public bool naturallyPlaced = false;
}
