using System.Collections;
using UnityEngine;

[System.Serializable]
public class BiomeClass
{
    public string name;
    public Color biomeCol;
    public TileAtlas tileAtlas;

    [Header("Noise Settings")]
    public Texture2D caveNoiseTexture;

    [Header("Generation Settings")]
    public bool generateCaves = true;
    public int dirtLayerHight = 5;
    public float surfaceValue = 0.25f;
    public float heightMultiplier = 20f;

    [Header("Trees")]
    public int treeChance = 15;
    public int minTreeHeight = 5;
    public int maxTreeHeight = 9;

    [Header("Addons")]
    public int tallGrassChance = 10;

    [Header("Ore Settings")]
    public OreClass[] ores;
}
