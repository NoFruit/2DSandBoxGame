using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrianGeneration : MonoBehaviour
{
    /*
     * 方块纹理素材
     */
    [Header("Tile Sprites")]
    public float seed;
    public TileAtlas tileAtlas;

    /*
     * 生物群系类
     */
    public BiomeClass[] biomes;



    /*
     * [Trees]树木部分
     * treeChance: 树木出现概率，概率是自身倒数;
     * minTreeHeight: 最小树高；
     * maxTreeHeight: 最大树高；
     * 
     *  [Header("Biomes")]生物群系
     *  public Color grassland;
     *  public Color desert;
     *  public Color snow;
     *  public Color forest;
     *  颜色代表不同生物群系
     *  public Texture2D biomeMap: 生物群系噪声纹理；
     * 
     * [Generation Settings]
     * chunkSize: 区块大小；
     * worldSize: 地图大小；
     * generateCaves: 测试地形是否生成正确，排除洞穴干扰；
     * dirtLayerHight: 土层高度；
     * surfaceValue: 地形出现的阈值，关乎地形复杂度，有方块区域面积；
     * heightMultiplier: 地形高度缩放系数，地形的乘数；
     * heightAddition: 地形高度基础增量，地形的加数；
     * 
     * [Noise Settings]随机噪声部分
     * terrainFreq: 地形凹凸程度；
     * caveFreq: 洞穴出现概率，分布面积，低代表单个大，频率高；
     * seed: 随机数，随机种子；
     * noiseTexture: 噪声纹理；
     * 
     * [Ore Settings]矿石部分
     * 一个矿石类的列表
     * 
     * worldChunks：维护区块列表
     * worldTiles: 记录地图某坐标是否有方块；
     */

    //[Header("Trees")]
    //public int treeChance = 15;
    //public int minTreeHeight = 5;
    //public int maxTreeHeight = 9;

    //[Header("Addons")]
    //public int tallGrassChance = 10;

    [Header("Biomes")]
    public float biomeFreq;
    public Gradient biomeGradient;
    public Texture2D biomeMap;

    [Header("Generation Settings")]
    public int chunkSize = 16;
    public int worldSize = 100;
    public bool generateCaves = true;
    // public int dirtLayerHight = 5;
    // public float surfaceValue = 0.25f;
    // public float heightMultiplier = 20f;
    public int heightAddition = 25;

    [Header("Noise Settings")]
    // public float terrainFreq = 0.05f;
    public float caveFreq = 0.05f;
    public Texture2D caveNoiseTexture;

    [Header("Ore Settings")]
    public OreClass[] ores;

    private GameObject[] worldChunks;
    private List<Vector2> worldTiles = new List<Vector2>();
    private BiomeClass curBiome;


    private void Start()
    {
        for (int i = 0; i < ores.Length; i++)
        {
            ores[i].spreadTexture = new Texture2D(worldSize, worldSize);
        }

        // 随机种子生成
        seed = Random.Range(-10000, 10000);

        // TODO 由于铁和煤类似的噪声分布，把随机算法变得复杂一些或者为它们生成特定的种子

        DrawTextures();
        DrawCavesAndOres();

        // 创建地形
        CreateChunks();
        GenerateTerrain();
    }

    public void DrawCavesAndOres()
    {
        caveNoiseTexture = new Texture2D(worldSize, worldSize);

        for (int x = 0; x < worldSize; x++)
        {
            for (int y = 0; y < worldSize; y++)
            {
                curBiome = GetCurrentBiome(x, y);
                float v = Mathf.PerlinNoise((x + seed) * curBiome.caveFreq, (y + seed) * curBiome.caveFreq);
                if (v > curBiome.surfaceValue)
                    caveNoiseTexture.SetPixel(x, y, Color.white);
                else
                    caveNoiseTexture.SetPixel(x, y, Color.black);
            }
        }

        caveNoiseTexture.Apply();

        for (int x = 0; x < worldSize; x++)
        {
            for (int y = 0; y < worldSize; y++)
            {
                curBiome = GetCurrentBiome(x, y);
                for (int i = 0; i < ores.Length; i++)
                {
                    ores[i].spreadTexture.SetPixel(x, y, Color.black);

                    float v = Mathf.PerlinNoise((x + seed) * curBiome.ores[i].frequency, (y + seed) * curBiome.ores[i].frequency);
                    if (v > curBiome.ores[i].size)
                        ores[i].spreadTexture.SetPixel(x, y, Color.white);
                }
            }

        }

        foreach (var ore in ores)
        {
            ore.spreadTexture.Apply();
        }
    }

    public void DrawTextures()
    {
        // 生物群系纹理生成
        biomeMap = new Texture2D(worldSize, worldSize);
        DrawBiomeTexture();

        for (int i = 0; i < biomes.Length; i++)
        {

            // 洞穴纹理生成
            GenerateNoiseTexture(biomes[i].caveFreq, biomes[i].surfaceValue, ref biomes[i].caveNoiseTexture);

            // 矿物纹理生成
            for (int o = 0; o < biomes[i].ores.Length; o++)
            {
                GenerateNoiseTexture(biomes[i].ores[o].frequency, biomes[i].ores[o].size, ref biomes[i].ores[o].spreadTexture);
            }
        }
    }

    public void DrawBiomeTexture()
    {
        for (int x = 0; x < biomeMap.width; x++)
        {
            for (int y = 0; y < biomeMap.height; y++)
            {
                // 在颜色部分使用梯度来表示颜色
                float v = Mathf.PerlinNoise((x + seed) * biomeFreq, (y + seed) * biomeFreq);
                Color col = biomeGradient.Evaluate(v);
                biomeMap.SetPixel(x, y, col);
               
            }
        }

        biomeMap.Apply();
    }

    private void CreateChunks()
    {
        int numChunks = worldSize / chunkSize;
        worldChunks = new GameObject[numChunks];

        for (int i = 0; i < numChunks; i++)
        {
            GameObject newChunk = new GameObject();
            newChunk.name = i.ToString();
            newChunk.transform.parent = this.transform;
            worldChunks[i] = newChunk;
        }

    }

    public BiomeClass GetCurrentBiome(int x, int y)
    {
        // 改变curBiome的值

        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i].biomeCol == biomeMap.GetPixel(x, y))
            {
                return biomes[i];
            }
        }

        return curBiome;
    }


    private void GenerateTerrain()
    {
        Sprite[] tileSprites;
        for(int x = 0; x < worldSize; x++)
        {
            curBiome = GetCurrentBiome(x, 0);
            // 在当前的x坐标计算地形高度，Perlin噪声可以在不同循环保持高度值平滑 
            float height = Mathf.PerlinNoise((x + seed) * curBiome.terrainFreq, seed * curBiome.terrainFreq) * curBiome.heightMultiplier + heightAddition;

            for(int y = 0; y < height; y++)
            {
                /* 根据当前高度改变地形方块
                 * 地形的每一列：
                 *      第一层草方块
                 *      第二层土方块
                 *      第三层石头方块
                 *          - 石头方块中，根据矿石的稀有度从小到大生成
                 * 
                 * 根据不同的生物群系随机，选择对应的方块集
                 */
                // 根据生物群系颜色
                curBiome = GetCurrentBiome(x, y);

                if (y < height - curBiome.dirtLayerHight)
                {
                    tileSprites = curBiome.tileAtlas.stone.tileSprites;

                    if (ores[0].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[0].maxSpawnHeight)
                        tileSprites = tileAtlas.coal.tileSprites;
                    if (ores[1].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[1].maxSpawnHeight)
                        tileSprites = tileAtlas.iron.tileSprites;
                    if (ores[2].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[2].maxSpawnHeight)
                        tileSprites = tileAtlas.gold.tileSprites;
                    if (ores[3].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[3].maxSpawnHeight)
                        tileSprites = tileAtlas.diamond.tileSprites;
                }
                else if (y < height - 1)
                {
                    tileSprites = curBiome.tileAtlas.dirt.tileSprites;
                }
                else
                {
                    tileSprites = curBiome.tileAtlas.grass.tileSprites;
                }

                // 根据噪声纹理确定当前坐标有没有洞穴
                // 最后放置方块
                if (generateCaves)
                {
                    if (caveNoiseTexture.GetPixel(x, y).r > 0.5f)
                    {
                        PlaceTile(tileSprites, x, y);
                    }
                }
                else
                {
                    PlaceTile(tileSprites, x, y);
                }

                // 生成到达地面表层后
                // 树地形从草皮表层继续生成，代码至此往下生成树

                if (y >= height - 1)
                {
                    int t = Random.Range(0, curBiome.treeChance);

                    if (t == 1)
                    {
                        // 在当前有方块为支撑底部的地方
                        if (worldTiles.Contains(new Vector2(x, y)))
                        {
                            // 生成树
                            GenerateTree(Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);
                        }
                    }
                    else
                    {
                        // 同样的情况下有机会生成草
                        int i = Random.Range(0, curBiome.tallGrassChance);

                        if (i == 1)
                        {
                            if (worldTiles.Contains(new Vector2(x, y)))
                            {
                                if (curBiome.tileAtlas.tallGrass != null)
                                {
                                    PlaceTile(curBiome.tileAtlas.tallGrass.tileSprites, x, y + 1);
                                }
                            }
                        }
                    }
                }
            }
        }

    }

    /* 
     * 参数：
     * frequency：特定随机概率
     * limit: 表面值
     * noiseTexture：特定随机纹理
     * 
     * 可以生成对应需求的地形的噪声纹理，让它们聚集
     * 对于任何超过表面值的噪声像素，将其设置为纯白
     * 纹理中纯白部分是地形会生成的位置
     */
    private void GenerateNoiseTexture(float frequency, float limit, ref Texture2D noiseTexture)
    {
        noiseTexture = new Texture2D(worldSize, worldSize);

        for(int x = 0; x < noiseTexture.width; x++)
        {
            for(int y = 0; y < noiseTexture.height; y++)
            {
                // create a 2D texture
                float v = Mathf.PerlinNoise((x + seed) * frequency, (y + seed) * frequency);
                if (v > limit)
                    noiseTexture.SetPixel(x, y, Color.white);
                else
                    noiseTexture.SetPixel(x, y, Color.black);
            }
        }

        noiseTexture.Apply();
    }

    /*
     * 参数：
     * (x, y)坐标
     * 
     * 在这个坐标的位置向上拔地而起一棵树
     */
    public void GenerateTree(int treeHeight, int x, int y)
    {
        // 把树做成树样
        for (int i = 0; i < treeHeight; i++)
        {
            PlaceTile(tileAtlas.log.tileSprites, x, y + i);
        }

        // 添加叶子
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight);
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight + 1);
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight + 2);

        PlaceTile(tileAtlas.leaf.tileSprites, x - 1, y + treeHeight);
        PlaceTile(tileAtlas.leaf.tileSprites, x - 1, y + treeHeight + 1);

        PlaceTile(tileAtlas.leaf.tileSprites, x + 1, y + treeHeight);
        PlaceTile(tileAtlas.leaf.tileSprites, x + 1, y + treeHeight + 1);

        // TODO 根据树叶子大小生成不同大小的形状
    }

    /*
     * 参数
     * tileSprite: 对应方块Sprite类
     * (x, y)坐标
     * 
     * 在特定位置生成对应的方块，并把方块放入合适的区块中
     * 并在列表中记录方块坐标，记录该位置有方块
     */
    public void PlaceTile(Sprite[] tileSprites, int x, int y)
    {
        if (!worldTiles.Contains(new Vector2Int(x, y)))
        {
            GameObject newTile = new GameObject();

            // 增加区块概念，将当前tile放置到对应的chunk
            int chunkCoord = Mathf.RoundToInt(Mathf.Round(x / chunkSize) * chunkSize);
            chunkCoord /= chunkSize;

            newTile.transform.parent = worldChunks[chunkCoord].transform;  // 添加为对应区块的子对象

            newTile.AddComponent<SpriteRenderer>(); // 添加Sprite Renderer

            int spriteIndex = Random.Range(0, tileSprites.Length);
            newTile.GetComponent<SpriteRenderer>().sprite = tileSprites[spriteIndex];   // 组件增加sprite

            newTile.name = tileSprites[0].name;
            newTile.transform.position = new Vector2(x + 0.5f, y + 0.5f);

            worldTiles.Add(newTile.transform.position - (Vector3.one * 0.5f));
        }
    }
}
