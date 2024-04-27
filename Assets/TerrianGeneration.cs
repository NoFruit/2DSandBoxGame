using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrianGeneration : MonoBehaviour
{
    public PlayerController player;
    public CamController cam;

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


    [Header("Biomes")]
    public float biomeFreq;
    public Gradient biomeGradient;
    public Texture2D biomeMap;

    [Header("Generation Settings")]
    public int chunkSize = 16;
    public int worldSize = 100;
    public bool generateCaves = true;
    public int heightAddition = 25;

    [Header("Noise Settings")]
    public Texture2D caveNoiseTexture;
    public float terrainFreq = 0.05f;
    public float caveFreq = 0.05f;

    [Header("Ore Settings")]
    public OreClass[] ores;

    private GameObject[] worldChunks;

    public List<Vector2> worldTiles = new List<Vector2>();
    public List<GameObject> worldTilesObjects = new List<GameObject>();

    private BiomeClass curBiome;
    private Color[] biomeCols;


    private void Start()
    {
        // 随机种子生成
        seed = Random.Range(-10000, 10000);

        for (int i = 0; i < ores.Length; i++)
        {
            ores[i].spreadTexture = new Texture2D(worldSize, worldSize);
        }

        biomeCols = new Color[biomes.Length];

        for (int i = 0; i < biomes.Length; i++)
        {
            biomeCols[i] = biomes[i].biomeCol;
        }

        // TODO 由于铁和煤类似的噪声分布，把随机算法变得复杂一些或者为它们生成特定的种子
        DrawBiomeMap();
        DrawTextures();
        DrawCavesAndOres();

        // 创建地形
        CreateChunks();
        GenerateTerrain();

        cam.worldSize = worldSize;
        cam.Spawn(new Vector3(player.spawnPos.x, player.spawnPos.y, cam.transform.position.z));
        player.Spawn();
    }

    private void Update()
    {
        RefreshChunks();
    }

    void RefreshChunks()
    {
        for(int i = 0; i < worldChunks.Length; i++)
        {
            if (Vector2.Distance(new Vector2((i * chunkSize) + (chunkSize / 2), 0), new Vector2(player.transform.position.x, 0)) > Camera.main.orthographicSize * 4f)
                worldChunks[i].SetActive(false);
            else
                worldChunks[i].SetActive(true);
        }
    }

    public void DrawBiomeMap()
    {
        float b;
        Color col;
        biomeMap = new Texture2D(worldSize, worldSize);

        for(int x = 0; x < biomeMap.width; x++)
        {
            for (int y = 0; y < biomeMap.height; y++)
            {
                b = Mathf.PerlinNoise((x + seed) * biomeFreq, (y + seed) * biomeFreq);
                col = biomeGradient.Evaluate(b);
                biomeMap.SetPixel(x, y, col);
            }
        }

        biomeMap.Apply();
    }

    public void DrawCavesAndOres()
    {
        caveNoiseTexture = new Texture2D(worldSize, worldSize);
        float v;
        float o;

        for (int x = 0; x < worldSize; x++)
        {
            for (int y = 0; y < worldSize; y++)
            {
                curBiome = GetCurrentBiome(x, y);
                v = Mathf.PerlinNoise((x + seed) * caveFreq, (y + seed) * caveFreq);
                if (v > curBiome.surfaceValue)
                    caveNoiseTexture.SetPixel(x, y, Color.white);
                else
                    caveNoiseTexture.SetPixel(x, y, Color.black);

                for (int i = 0; i < ores.Length; i++)
                {
                    ores[i].spreadTexture.SetPixel(x, y, Color.black);

                    o = Mathf.PerlinNoise((x + seed) * curBiome.ores[i].frequency, (y + seed) * curBiome.ores[i].frequency);
                    if (o > curBiome.ores[i].size)
                        ores[i].spreadTexture.SetPixel(x, y, Color.white);
                }
            }
        }

        caveNoiseTexture.Apply();

        foreach (var ore in ores)
        {
            ore.spreadTexture.Apply();
        }
    }

    public void DrawTextures()
    {
        for (int i = 0; i < biomes.Length; i++)
        {
            biomes[i].caveNoiseTexture = new Texture2D(worldSize, worldSize);
            for (int o = 0; o < biomes[i].ores.Length; o++)
            {
                biomes[i].ores[o].spreadTexture = new Texture2D(worldSize, worldSize);
                GenerateNoiseTextures(biomes[i].ores[o].frequency, biomes[i].ores[o].size, ref biomes[i].ores[o].spreadTexture);

            }
        }
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

        //for (int i = 0; i < biomes.Length; i++)
        //{
        //    if (biomes[i].biomeCol == biomeMap.GetPixel(x, y))
        //    {
        //        return biomes[i];
        //    }
        //}
        if (System.Array.IndexOf(biomeCols, biomeMap.GetPixel(x, y)) >= 0)
            return biomes[System.Array.IndexOf(biomeCols, biomeMap.GetPixel(x, y))];

        return curBiome;
    }


    private void GenerateTerrain()
    {
        Sprite[] tileSprites;
        for(int x = 0; x < worldSize; x++)
        {
            // 在当前的x坐标计算地形高度，Perlin噪声可以在不同循环保持高度值平滑 
            float height;

            for(int y = 0; y < worldSize; y++)
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
                height = Mathf.PerlinNoise((x + seed) * terrainFreq, seed * terrainFreq) * curBiome.heightMultiplier + heightAddition;

                if (x == worldSize / 2)
                    player.spawnPos = new Vector2(x, height + 2);

                curBiome = GetCurrentBiome(x, y);

                if (y >= height) { break; }

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
                        PlaceTile(tileSprites, x, y, false);
                    }
                }
                else
                {
                    PlaceTile(tileSprites, x, y, false);
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
                            if (curBiome.name == "Desert")
                            {
                                GenerateCactus(curBiome.tileAtlas, Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);
                            }
                            else
                            {
                                // 生成树
                                GenerateTree(Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);
                            }
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
                                    PlaceTile(curBiome.tileAtlas.tallGrass.tileSprites, x, y + 1, true);
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
    private void GenerateNoiseTextures(float frequency, float limit, ref Texture2D noiseTexture)
    {
        float v;
        noiseTexture = new Texture2D(worldSize, worldSize);

        for(int x = 0; x < noiseTexture.width; x++)
        {
            for(int y = 0; y < noiseTexture.height; y++)
            {
                // create a 2D texture
                v = Mathf.PerlinNoise((x + seed) * frequency, (y + seed) * frequency);

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
            PlaceTile(tileAtlas.log.tileSprites, x, y + i, true);
        }

        // 添加叶子
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight, true);
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight + 1, true);
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight + 2, true);

        PlaceTile(tileAtlas.leaf.tileSprites, x - 1, y + treeHeight, true);
        PlaceTile(tileAtlas.leaf.tileSprites, x - 1, y + treeHeight + 1, true);

        PlaceTile(tileAtlas.leaf.tileSprites, x + 1, y + treeHeight, true);
        PlaceTile(tileAtlas.leaf.tileSprites, x + 1, y + treeHeight + 1, true);

        // TODO 根据树叶子大小生成不同大小的形状
    }

    public void GenerateCactus(TileAtlas atlas, int treeHeight, int x, int y)
    {
        for (int i = 0; i < treeHeight; i++)
        {
            PlaceTile(atlas.log.tileSprites, x, y + i, true);
        }
    }

    /*
     * 参数
     * tileSprite: 对应方块Sprite类
     * (x, y)坐标
     * 
     * 在特定位置生成对应的方块，并把方块放入合适的区块中
     * 并在列表中记录方块坐标，记录该位置有方块
     */
    public void PlaceTile(Sprite[] tileSprites, int x, int y, bool backgroundElement)
    {
        if (!worldTiles.Contains(new Vector2Int(x, y)) && x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            GameObject newTile = new GameObject();

            // 增加区块概念，将当前tile放置到对应的chunk
            int chunkCoord = Mathf.RoundToInt(Mathf.Round(x / chunkSize) * chunkSize);
            chunkCoord /= chunkSize;

            newTile.transform.parent = worldChunks[chunkCoord].transform;  // 添加为对应区块的子对象

            newTile.AddComponent<SpriteRenderer>(); // 添加Sprite Renderer
            if (!backgroundElement)
            {
                newTile.AddComponent<BoxCollider2D>();
                newTile.GetComponent<BoxCollider2D>().size = Vector2.one;
                newTile.tag = "Ground";
            }

            int spriteIndex = Random.Range(0, tileSprites.Length);
            newTile.GetComponent<SpriteRenderer>().sprite = tileSprites[spriteIndex];   // 组件增加sprite
            newTile.GetComponent<SpriteRenderer>().sortingOrder = -5;

            newTile.name = tileSprites[0].name;
            newTile.transform.position = new Vector2(x + 0.5f, y + 0.5f);

            worldTiles.Add(newTile.transform.position - (Vector3.one * 0.5f));
            worldTilesObjects.Add(newTile);
        }
    }

    public void RemoveTile(int x, int y)
    {
        if (worldTiles.Contains(new Vector2Int(x, y)) && x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            Destroy(worldTilesObjects[worldTiles.IndexOf(new Vector2(x, y))]);
        }
    }

}
