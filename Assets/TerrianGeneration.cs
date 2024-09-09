using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrianGeneration : MonoBehaviour
{
    [Header("Lighting")]
    // Tile light
    public Texture2D worldTilesMap;
    public Material lightShader;
    public float groundLightThreshold = 0.7f;
    public float airLightThreshold = 0.85f;
    public float lightRadius = 7f;
    public List<Vector2Int> unlitBlocks = new List<Vector2Int>();

    public PlayerController player;
    public CamController cam;
    public GameObject tileDrop;

    [Header("Tile Sprites")]
    public float seed;
    public TileAtlas tileAtlas;
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

    // 区块父类列表
    private GameObject[] worldChunks;

    // 方块资源总存储结构，分别是gameobject和tile class
    private GameObject[,] world_ForegroundObjects;
    private GameObject[,] world_BackgroundObjects;
    private TileClass[,] world_ForegroundTiles;
    private TileClass[,] world_BackgroundTiles;

    private BiomeClass curBiome;
    private Color[] biomeCols;


    private void Start()
    {
        // 方块类管理区初始化
        world_ForegroundObjects = new GameObject[worldSize, worldSize];
        world_BackgroundObjects = new GameObject[worldSize, worldSize];
        world_ForegroundTiles = new TileClass[worldSize, worldSize];
        world_BackgroundTiles = new TileClass[worldSize, worldSize];

        // 光照变量初始化
        worldTilesMap = new Texture2D(worldSize, worldSize)
        {
            filterMode = FilterMode.Point
        };
        lightShader.SetTexture("_ShadowTex", worldTilesMap);

        for (int x = 0; x < worldSize; x++)
        {
            for (int y = 0; y < worldSize; y++)
            {
                // 设置每个像素初始白色
                worldTilesMap.SetPixel(x, y, Color.white);
            }
        }
        worldTilesMap.Apply();


        // 地形生成
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

        DrawBiomeMap();
        DrawTextures();
        DrawCavesAndOres();

        // 创建地形
        CreateChunks();
        GenerateTerrain();

        // 自然光照
        for (int x = 0; x < worldSize; x++)
        {
            for (int y = 0; y < worldSize; y++)
            {
                if (worldTilesMap.GetPixel(x, y) == Color.white)
                    LightBlock(x, y, 1f, 0);
            }
        }

        worldTilesMap.Apply();

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
        for (int i = 0; i < worldChunks.Length; i++)
        {
            if (Vector2.Distance(new Vector2((i * chunkSize) + (chunkSize / 2), 0), 
                    new Vector2(player.transform.position.x, 0)) > Camera.main.orthographicSize * 6f)
                worldChunks[i].SetActive(false);
            else
                worldChunks[i].SetActive(true);
        }
    }

    /*
     * 纹理绘制几个方法：
     * 
     * 最先绘制生物群系纹理
     * 根据当前像素的生物群系，
     * 指导后续的洞穴和矿物纹理
     */
    public void DrawBiomeMap()
    {
        float b;
        Color col;
        biomeMap = new Texture2D(worldSize, worldSize);

        for (int x = 0; x < biomeMap.width; x++)
        {
            for (int y = 0; y < biomeMap.height; y++)
            {
                // 通过平滑的噪声值b在颜色盘寻找内容，不超过阈值的颜色会凑在一起
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

    /*
     * 对每个生物群系的每个矿物内容、洞穴内容进行生成
     */
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

        for (int x = 0; x < noiseTexture.width; x++)
        {
            for (int y = 0; y < noiseTexture.height; y++)
            {
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
     *  正式地形生成部分
     */
    private void CreateChunks()
    {
        int numChunks = worldSize / chunkSize;
        worldChunks = new GameObject[numChunks];

        for (int i = 0; i < numChunks; i++)
        {
            GameObject newGameObject = new GameObject();
            GameObject newChunk = newGameObject;
            newChunk.name = i.ToString();
            newChunk.transform.parent = this.transform;
            worldChunks[i] = newChunk;
        }

    }

    public BiomeClass GetCurrentBiome(int x, int y)
    {
        // TODO
        // 改变curBiome的值

        if (System.Array.IndexOf(biomeCols, biomeMap.GetPixel(x, y)) >= 0)
            return biomes[System.Array.IndexOf(biomeCols, biomeMap.GetPixel(x, y))];

        return curBiome;
    }


    private void GenerateTerrain()
    {
        TileClass tileClass;
        for (int x = 0; x < worldSize - 1; x++)
        {
            // 在当前的x坐标计算地形高度，Perlin噪声可以在不同循环保持高度值平滑 
            float height;

            /* 根据当前高度改变地形方块
             * 地形的每一列：
             *      第一层草方块
             *      第二层土方块
             *      第三层石头方块
             *          - 石头方块中，根据矿石的稀有度从小到大生成
             * 
             * 根据不同的生物群系随机，选择对应的方块集
             */
            for (int y = 0; y < worldSize; y++)
            {
                // 根据生物群系颜色,制造高度
                height = Mathf.PerlinNoise((x + seed) * terrainFreq, seed * terrainFreq) * curBiome.heightMultiplier + heightAddition;

                if (x == worldSize / 2)
                    player.spawnPos = new Vector2(x, height + 2);

                curBiome = GetCurrentBiome(x, y);

                if (y >= height) { break; }

                if (y < height - curBiome.dirtLayerHight)
                {
                    tileClass = curBiome.tileAtlas.stone;

                    if (ores[0].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[0].maxSpawnHeight)
                        tileClass = tileAtlas.coal;
                    if (ores[1].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[1].maxSpawnHeight)
                        tileClass = tileAtlas.iron;
                    if (ores[2].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[2].maxSpawnHeight)
                        tileClass = tileAtlas.gold;
                    if (ores[3].spreadTexture.GetPixel(x, y).r > 0.5f && height - y > ores[3].maxSpawnHeight)
                        tileClass = tileAtlas.diamond;
                }
                else if (y < height - 1)
                    tileClass = curBiome.tileAtlas.dirt;
                else
                    tileClass = curBiome.tileAtlas.grass;

                // 根据噪声纹理确定当前坐标有没有洞穴
                // 最后放置方块
                if (generateCaves)
                {
                    if (caveNoiseTexture.GetPixel(x, y).r > 0.5f)
                        PlaceTile(tileClass, x, y, true);
                    else if (tileClass.wallVariant != null)
                        PlaceTile(tileClass.wallVariant, x, y, true);
                }
                else
                {
                    PlaceTile(tileClass, x, y, true);
                }

                // 生成到达地面表层后
                // 树地形从草皮表层继续生成，代码至此往下生成树
                if (y >= height - 1)
                {
                    int t = Random.Range(0, curBiome.treeChance);

                    if (t == 1)
                    {
                        // 在当前有方块为支撑底部的地方
                        if (GetTileFromWorld(x, y))
                        {
                            if (curBiome.name == "Desert")
                                GenerateCactus(curBiome.tileAtlas, Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);
                            else
                                GenerateTree(Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);   // 生成树
                        }
                    }
                    else
                    {
                        // 同样的情况下有机会生成草
                        int i = Random.Range(0, curBiome.tallGrassChance);

                        if (i == 1)
                        {
                            if (GetTileFromWorld(x, y))
                            {
                                if (curBiome.tileAtlas.tallGrass != null)
                                    PlaceTile(curBiome.tileAtlas.tallGrass, x, y + 1, true);
                            }
                        }
                    }
                }
            }
        }

        worldTilesMap.Apply();

    }


    /*
     * 参数：
     * (x, y)坐标
     * 
     * 在这个坐标的位置向上拔地而起一棵树
     * 树和仙人掌的生成是侵占性的，
     * 需要用CheckTile确保不会和别的方块生成到一起
     */
    public void GenerateTree(int treeHeight, int x, int y)
    {
        // 把树做成树样
        for (int i = 0; i < treeHeight; i++)
        {
            CheckTile(tileAtlas.log, x, y + i, true);
        }

        // 添加叶子
        CheckTile(tileAtlas.leaf, x, y + treeHeight, true);
        CheckTile(tileAtlas.leaf, x, y + treeHeight + 1, true);
        CheckTile(tileAtlas.leaf, x, y + treeHeight + 2, true);

        CheckTile(tileAtlas.leaf, x - 1, y + treeHeight, true);
        CheckTile(tileAtlas.leaf, x - 1, y + treeHeight + 1, true);

        CheckTile(tileAtlas.leaf, x + 1, y + treeHeight, true);
        CheckTile(tileAtlas.leaf, x + 1, y + treeHeight + 1, true);

        // TODO 根据树叶子大小生成不同大小的形状
    }

    public void GenerateCactus(TileAtlas atlas, int treeHeight, int x, int y)
    {
        for (int i = 0; i < treeHeight; i++)
        {
            CheckTile(atlas.log, x, y + i, true);
        }
    }

    /*
     * 参数:
     * tile: 方块
     * (x, y): 坐标
     * isNaturallyPlaced: 自然放置
     * 
     * 玩家或特殊的放置操作
     * 
     * 当主动进行放置操作时，
     * 首先是正常进行放置操作，正常放置的方块附近必须有其他方块
     * 同时增加如若背景墙中正有内容，将背景墙即刻破坏的分支
     */
    public void CheckTile(TileClass tile, int x, int y, bool isNaturallyPlaced)
    {
        if (x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            // 背景方块只能放空白区域
            if (tile.isBackground)
            {
                if (!GetTileFromWorld(x, y))
                {
                    RemoveLightSource(x, y);
                    PlaceTile(tile, x, y, isNaturallyPlaced);
                }
            }
            else
            {
                // 普通方块只能相邻别的墙体或实体
                if (HasNeighbor(x, y))
                {
                    // 当前位置无tile类直接放置
                    // 当前位置有背景tile类覆盖在上面
                    if (!GetTileFromWorld(x, y))
                    {
                        RemoveLightSource(x, y);
                        PlaceTile(tile, x, y, isNaturallyPlaced);
                    }
                    else
                    {
                        if (GetTileFromWorld(x, y).isBackground)
                        {
                            RemoveLightSource(x, y);
                            PlaceTile(tile, x, y, isNaturallyPlaced);
                        }
                    }
                }

            }
        }
    }

    // 判断四周是否有tile类
    bool HasNeighbor(int x, int y)
    {
        int[,] offset = new int[,] { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        for (int i = 0; i < 4; i++)
        {
            int nx = x + offset[i, 0];
            int ny = y + offset[i, 1];
            if (GetTileFromWorld(nx, ny))
            {
                return true;
            }
        }
        return false;
    }

    /*
     * 参数
     * tile: 需要被放置的方块类
     * (x, y)坐标
     * isNaturallyPlaced: 自然放置
     * 
     * 在特定位置生成对应的方块，并把方块放入合适的区块中
     * 并在列表中记录方块坐标，记录该位置有方块
     */
    public void PlaceTile(TileClass tile, int x, int y, bool isNaturallyPlaced)
    {
        if (x >= 0 && x < worldSize && y >= 0 && y < worldSize)
        {
            GameObject newGameObject = new GameObject();
            GameObject newTileObject = newGameObject;

            // gameobject基本属性
            newTileObject.name = tile.tileSprites[0].name;
            newTileObject.transform.position = new Vector2(x + 0.5f, y + 0.5f);

            // 将当前tile放置到对应的chunk
            int chunkCoord = Mathf.RoundToInt(Mathf.Round(x / chunkSize) * chunkSize);
            chunkCoord /= chunkSize;
            newTileObject.transform.parent = worldChunks[chunkCoord].transform;  // 添加为对应区块的子对象

            // 给当前tile添加Sprite
            // Sprite可能有变种,随机选择
            newTileObject.AddComponent<SpriteRenderer>();
            int spriteIndex = Random.Range(0, tile.tileSprites.Length);
            newTileObject.GetComponent<SpriteRenderer>().sprite = tile.tileSprites[spriteIndex];

            // 光照部分根据透明度改变
            if (tile.isPassLight)
                worldTilesMap.SetPixel(x, y, Color.white);
            else
                worldTilesMap.SetPixel(x, y, Color.black);

            // 背景前景区分
            if (tile.isBackground)
            {
                // 背景非实体方块
                // 背景墙颜色更深以便区分
                newTileObject.GetComponent<SpriteRenderer>().sortingOrder = -10;
                if (tile.name.ToLower().Contains("wall"))
                    newTileObject.GetComponent<SpriteRenderer>().color = new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                // 前景实体方块
                newTileObject.GetComponent<SpriteRenderer>().sortingOrder = -5;
                newTileObject.AddComponent<BoxCollider2D>();
                newTileObject.GetComponent<BoxCollider2D>().size = Vector2.one;
                newTileObject.tag = "Ground";
            }

            //方块总存储结构更新
            TileClass newTileClass = TileClass.CreateInstance(tile, isNaturallyPlaced);
            AddTileToWorld(x, y, newTileClass);
            AddObjectToWorld(x, y, newTileObject, newTileClass);

        }
    }



    public void RemoveTile(int x, int y)
    {
        if (GetTileFromWorld(x, y) && x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            TileClass currentTileClass = GetTileFromWorld(x, y);

            // 破坏GameObject
            Destroy(GetObjectFromWorld(x, y));

            // 方块掉落功能
            if (currentTileClass.tileDrop)
            {
                GameObject newtileDrop = Instantiate(tileDrop, new Vector2(x, y + 0.5f), Quaternion.identity);
                newtileDrop.GetComponent<SpriteRenderer>().sprite = currentTileClass.tileDrop;
            }

            // 方块总存储结构更新
            RemoveTileFromWorld(x, y);
            RemoveObjectFromWorld(x, y);

            // 自然生成方块被破坏,将背景变体放置在当前坐标
            if (currentTileClass.wallVariant != null && currentTileClass.naturallyPlaced)
            {
                RemoveLightSource(x, y);
                PlaceTile(currentTileClass.wallVariant, x, y, true);
            }

            // 此处无任何方块,认为是光源
            // 否则遵循此函数外默认光照的结果(这里不需要动)
            if (!GetTileFromWorld(x, y))
            {
                worldTilesMap.SetPixel(x, y, Color.white);
                LightBlock(x, y, 1f, 0);
                worldTilesMap.Apply();
            }
        }
    }


    /*
        自动管理前景Tile和背景Tile的函数
        分别为:添加,删除,获取

        分别对GameObject资产和TileClass资产都有相关方法

        对单一坐标的Tile资产获取总是先获取前景后获取背景
    */
    void AddObjectToWorld(int x, int y, GameObject tileObject, TileClass tile)
    {
        if (tile.isBackground)
        {
            world_BackgroundObjects[x, y] = tileObject;
        }
        else
        {
            world_ForegroundObjects[x, y] = tileObject;
        }
    }

    void RemoveObjectFromWorld(int x, int y)
    {
        if (world_ForegroundObjects[x, y] != null)
        {
            world_ForegroundObjects[x, y] = null;
        }
        else if (world_BackgroundObjects[x, y] != null)
        {
            world_BackgroundObjects[x, y] = null;
        }
        // else if here is no tile: do nothing
    }

    GameObject GetObjectFromWorld(int x, int y)
    {
        if (world_ForegroundObjects[x, y] != null)
        {
            return world_ForegroundObjects[x, y];
        }
        else if (world_BackgroundObjects[x, y] != null)
        {
            return world_BackgroundObjects[x, y];
        }

        return null;
    }

    void AddTileToWorld(int x, int y, TileClass tile)
    {
        if (tile.isBackground)
        {
            world_BackgroundTiles[x, y] = tile;
        }
        else
        {
            world_ForegroundTiles[x, y] = tile;
        }
    }

    void RemoveTileFromWorld(int x, int y)
    {
        if (world_ForegroundTiles[x, y] != null)
        {
            world_ForegroundTiles[x, y] = null;
        }
        else if (world_BackgroundTiles[x, y] != null)
        {
            world_BackgroundTiles[x, y] = null;
        }
        // else if here is no tile: do nothing
    }

    TileClass GetTileFromWorld(int x, int y)
    {
        if (world_ForegroundTiles[x, y] != null)
        {
            return world_ForegroundTiles[x, y];
        }
        else if (world_BackgroundTiles[x, y] != null)
        {
            return world_BackgroundTiles[x, y];
        }

        return null;
    }



    /*
     * 光扩散函数
     * 参数：
     * (x, y): 当前位置
     * intensity: 单像素光照强度，强的亮
     * iteration: 迭代次数，比lightRadius小
     */
    void LightBlock(int x, int y, float intensity, int iteration)
    {
        if (iteration < lightRadius)
        {
            worldTilesMap.SetPixel(x, y, Color.white * intensity);

            float thresh = groundLightThreshold;
            if (x >= 0 && x < worldSize && y >= 0 && y < worldSize)
            {
                if (world_ForegroundTiles[x, y])
                    thresh = groundLightThreshold;
                else
                    thresh = airLightThreshold;
            }

            // nx is neighbor x, and also neighbor y
            // 处理当前坐标的周围光照
            for (int nx = x - 1; nx < x + 2; nx++)
            {
                for (int ny = y - 1; ny < y + 2; ny++)
                {
                    if (nx != x || ny != y)
                    {
                        // 根据距离计算新光照强度
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(nx, ny));
                        float targetIntensity = Mathf.Pow(thresh, dist) * intensity;
                        if (worldTilesMap.GetPixel(nx, ny) != null)
                        {
                            if (worldTilesMap.GetPixel(nx, ny).r < targetIntensity) // 只有比光暗的地方会被照亮
                                LightBlock(nx, ny, targetIntensity, iteration + 1);
                        }
                    }
                }
            }

            worldTilesMap.Apply();
        }
    }

    /*
     * 将坐标附近较亮的光记录并扩散
     * 破坏光线条件并重组
     */
    void RemoveLightSource(int x, int y)
    {
        // 灭光
        unlitBlocks.Clear();
        UnLightBlock(x, y, x, y);

        List<Vector2Int> toRelight = new List<Vector2Int>();

        // unlitBlocks中的每一个坐标，它们的邻居，遍历
        // unlitblocks周围有亮光比自己大的点，就会放入toRelight
        foreach (Vector2Int block in unlitBlocks)
        {
            for (int nx = block.x - 1; nx < block.x + 2; nx++)
            {
                for (int ny = block.y - 1; ny < block.y + 2; ny++)
                {
                    if (worldTilesMap.GetPixel(nx, ny) != null)
                    {
                        if (worldTilesMap.GetPixel(nx, ny).r > worldTilesMap.GetPixel(block.x, block.y).r)
                        {
                            if (!toRelight.Contains(new Vector2Int(nx, ny)))
                                toRelight.Add(new Vector2Int(nx, ny));
                        }
                    }
                }
            }
        }

        // 扩散较亮区域的亮光来照亮unlitBlocks
        foreach (Vector2Int source in toRelight)
        {
            LightBlock(source.x, source.y, worldTilesMap.GetPixel(source.x, source.y).r, 0);
        }

        worldTilesMap.Apply();
    }

    /*
     * 参数：
     * (x, y)：位置
     * (ix, iy)：initial 位置 
     * 
     * 灭光函数，以输入点为中心，光半径内变黑并记录为unlitBlocks
     */
    void UnLightBlock(int x, int y, int ix, int iy)
    {
        if (Mathf.Abs(x - ix) >= lightRadius || Mathf.Abs(y - iy) >= lightRadius || unlitBlocks.Contains(new Vector2Int(x, y)))
            return;

        // 周围亮度低于中心的,移除光照并记录
        for (int nx = x - 1; nx < x + 2; nx++)
        {
            for (int ny = y - 1; ny < y + 2; ny++)
            {
                if (nx != x || ny != y)
                {
                    if (worldTilesMap.GetPixel(nx, ny) != null)
                    {
                        if (worldTilesMap.GetPixel(nx, ny).r < worldTilesMap.GetPixel(x, y).r)
                            UnLightBlock(nx, ny, ix, iy);
                    }
                }
            }
        }

        worldTilesMap.SetPixel(x, y, Color.black);
        unlitBlocks.Add(new Vector2Int(x, y));
    }
}
