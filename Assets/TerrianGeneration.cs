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

    // ���鸸���б�
    private GameObject[] worldChunks;

    // ������Դ�ܴ洢�ṹ���ֱ���gameobject��tile class
    private GameObject[,] world_ForegroundObjects;
    private GameObject[,] world_BackgroundObjects;
    private TileClass[,] world_ForegroundTiles;
    private TileClass[,] world_BackgroundTiles;

    private BiomeClass curBiome;
    private Color[] biomeCols;


    private void Start()
    {
        // �������������ʼ��
        world_ForegroundObjects = new GameObject[worldSize, worldSize];
        world_BackgroundObjects = new GameObject[worldSize, worldSize];
        world_ForegroundTiles = new TileClass[worldSize, worldSize];
        world_BackgroundTiles = new TileClass[worldSize, worldSize];

        // ���ձ�����ʼ��
        worldTilesMap = new Texture2D(worldSize, worldSize)
        {
            filterMode = FilterMode.Point
        };
        lightShader.SetTexture("_ShadowTex", worldTilesMap);

        for (int x = 0; x < worldSize; x++)
        {
            for (int y = 0; y < worldSize; y++)
            {
                // ����ÿ�����س�ʼ��ɫ
                worldTilesMap.SetPixel(x, y, Color.white);
            }
        }
        worldTilesMap.Apply();


        // ��������
        // �����������
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

        // ��������
        CreateChunks();
        GenerateTerrain();

        // ��Ȼ����
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
     * ������Ƽ���������
     * 
     * ���Ȼ�������Ⱥϵ����
     * ���ݵ�ǰ���ص�����Ⱥϵ��
     * ָ�������Ķ�Ѩ�Ϳ�������
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
                // ͨ��ƽ��������ֵb����ɫ��Ѱ�����ݣ���������ֵ����ɫ�����һ��
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
     * ��ÿ������Ⱥϵ��ÿ���������ݡ���Ѩ���ݽ�������
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
     * ������
     * frequency���ض��������
     * limit: ����ֵ
     * noiseTexture���ض��������
     * 
     * �������ɶ�Ӧ����ĵ��ε��������������Ǿۼ�
     * �����κγ�������ֵ���������أ���������Ϊ����
     * �����д��ײ����ǵ��λ����ɵ�λ��
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
     *  ��ʽ�������ɲ���
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
        // �ı�curBiome��ֵ

        if (System.Array.IndexOf(biomeCols, biomeMap.GetPixel(x, y)) >= 0)
            return biomes[System.Array.IndexOf(biomeCols, biomeMap.GetPixel(x, y))];

        return curBiome;
    }


    private void GenerateTerrain()
    {
        TileClass tileClass;
        for (int x = 0; x < worldSize - 1; x++)
        {
            // �ڵ�ǰ��x���������θ߶ȣ�Perlin���������ڲ�ͬѭ�����ָ߶�ֵƽ�� 
            float height;

            /* ���ݵ�ǰ�߶ȸı���η���
             * ���ε�ÿһ�У�
             *      ��һ��ݷ���
             *      �ڶ���������
             *      ������ʯͷ����
             *          - ʯͷ�����У����ݿ�ʯ��ϡ�жȴ�С��������
             * 
             * ���ݲ�ͬ������Ⱥϵ�����ѡ���Ӧ�ķ��鼯
             */
            for (int y = 0; y < worldSize; y++)
            {
                // ��������Ⱥϵ��ɫ,����߶�
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

                // ������������ȷ����ǰ������û�ж�Ѩ
                // �����÷���
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

                // ���ɵ���������
                // �����δӲ�Ƥ���������ɣ�������������������
                if (y >= height - 1)
                {
                    int t = Random.Range(0, curBiome.treeChance);

                    if (t == 1)
                    {
                        // �ڵ�ǰ�з���Ϊ֧�ŵײ��ĵط�
                        if (GetTileFromWorld(x, y))
                        {
                            if (curBiome.name == "Desert")
                                GenerateCactus(curBiome.tileAtlas, Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);
                            else
                                GenerateTree(Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);   // ������
                        }
                    }
                    else
                    {
                        // ͬ����������л������ɲ�
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
     * ������
     * (x, y)����
     * 
     * ����������λ�����ϰεض���һ����
     * ���������Ƶ���������ռ�Եģ�
     * ��Ҫ��CheckTileȷ������ͱ�ķ������ɵ�һ��
     */
    public void GenerateTree(int treeHeight, int x, int y)
    {
        // ������������
        for (int i = 0; i < treeHeight; i++)
        {
            CheckTile(tileAtlas.log, x, y + i, true);
        }

        // ���Ҷ��
        CheckTile(tileAtlas.leaf, x, y + treeHeight, true);
        CheckTile(tileAtlas.leaf, x, y + treeHeight + 1, true);
        CheckTile(tileAtlas.leaf, x, y + treeHeight + 2, true);

        CheckTile(tileAtlas.leaf, x - 1, y + treeHeight, true);
        CheckTile(tileAtlas.leaf, x - 1, y + treeHeight + 1, true);

        CheckTile(tileAtlas.leaf, x + 1, y + treeHeight, true);
        CheckTile(tileAtlas.leaf, x + 1, y + treeHeight + 1, true);

        // TODO ������Ҷ�Ӵ�С���ɲ�ͬ��С����״
    }

    public void GenerateCactus(TileAtlas atlas, int treeHeight, int x, int y)
    {
        for (int i = 0; i < treeHeight; i++)
        {
            CheckTile(atlas.log, x, y + i, true);
        }
    }

    /*
     * ����:
     * tile: ����
     * (x, y): ����
     * isNaturallyPlaced: ��Ȼ����
     * 
     * ��һ�����ķ��ò���
     * 
     * ���������з��ò���ʱ��
     * �������������з��ò������������õķ��鸽����������������
     * ͬʱ������������ǽ���������ݣ�������ǽ�����ƻ��ķ�֧
     */
    public void CheckTile(TileClass tile, int x, int y, bool isNaturallyPlaced)
    {
        if (x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            // ��������ֻ�ܷſհ�����
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
                // ��ͨ����ֻ�����ڱ��ǽ���ʵ��
                if (HasNeighbor(x, y))
                {
                    // ��ǰλ����tile��ֱ�ӷ���
                    // ��ǰλ���б���tile�า��������
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

    // �ж������Ƿ���tile��
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
     * ����
     * tile: ��Ҫ�����õķ�����
     * (x, y)����
     * isNaturallyPlaced: ��Ȼ����
     * 
     * ���ض�λ�����ɶ�Ӧ�ķ��飬���ѷ��������ʵ�������
     * �����б��м�¼�������꣬��¼��λ���з���
     */
    public void PlaceTile(TileClass tile, int x, int y, bool isNaturallyPlaced)
    {
        if (x >= 0 && x < worldSize && y >= 0 && y < worldSize)
        {
            GameObject newGameObject = new GameObject();
            GameObject newTileObject = newGameObject;

            // gameobject��������
            newTileObject.name = tile.tileSprites[0].name;
            newTileObject.transform.position = new Vector2(x + 0.5f, y + 0.5f);

            // ����ǰtile���õ���Ӧ��chunk
            int chunkCoord = Mathf.RoundToInt(Mathf.Round(x / chunkSize) * chunkSize);
            chunkCoord /= chunkSize;
            newTileObject.transform.parent = worldChunks[chunkCoord].transform;  // ���Ϊ��Ӧ������Ӷ���

            // ����ǰtile���Sprite
            // Sprite�����б���,���ѡ��
            newTileObject.AddComponent<SpriteRenderer>();
            int spriteIndex = Random.Range(0, tile.tileSprites.Length);
            newTileObject.GetComponent<SpriteRenderer>().sprite = tile.tileSprites[spriteIndex];

            // ���ղ��ָ���͸���ȸı�
            if (tile.isPassLight)
                worldTilesMap.SetPixel(x, y, Color.white);
            else
                worldTilesMap.SetPixel(x, y, Color.black);

            // ����ǰ������
            if (tile.isBackground)
            {
                // ������ʵ�巽��
                // ����ǽ��ɫ�����Ա�����
                newTileObject.GetComponent<SpriteRenderer>().sortingOrder = -10;
                if (tile.name.ToLower().Contains("wall"))
                    newTileObject.GetComponent<SpriteRenderer>().color = new Color(0.6f, 0.6f, 0.6f);
            }
            else
            {
                // ǰ��ʵ�巽��
                newTileObject.GetComponent<SpriteRenderer>().sortingOrder = -5;
                newTileObject.AddComponent<BoxCollider2D>();
                newTileObject.GetComponent<BoxCollider2D>().size = Vector2.one;
                newTileObject.tag = "Ground";
            }

            //�����ܴ洢�ṹ����
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

            // �ƻ�GameObject
            Destroy(GetObjectFromWorld(x, y));

            // ������书��
            if (currentTileClass.tileDrop)
            {
                GameObject newtileDrop = Instantiate(tileDrop, new Vector2(x, y + 0.5f), Quaternion.identity);
                newtileDrop.GetComponent<SpriteRenderer>().sprite = currentTileClass.tileDrop;
            }

            // �����ܴ洢�ṹ����
            RemoveTileFromWorld(x, y);
            RemoveObjectFromWorld(x, y);

            // ��Ȼ���ɷ��鱻�ƻ�,��������������ڵ�ǰ����
            if (currentTileClass.wallVariant != null && currentTileClass.naturallyPlaced)
            {
                RemoveLightSource(x, y);
                PlaceTile(currentTileClass.wallVariant, x, y, true);
            }

            // �˴����κη���,��Ϊ�ǹ�Դ
            // ������ѭ�˺�����Ĭ�Ϲ��յĽ��(���ﲻ��Ҫ��)
            if (!GetTileFromWorld(x, y))
            {
                worldTilesMap.SetPixel(x, y, Color.white);
                LightBlock(x, y, 1f, 0);
                worldTilesMap.Apply();
            }
        }
    }


    /*
        �Զ�����ǰ��Tile�ͱ���Tile�ĺ���
        �ֱ�Ϊ:���,ɾ��,��ȡ

        �ֱ��GameObject�ʲ���TileClass�ʲ�������ط���

        �Ե�һ�����Tile�ʲ���ȡ�����Ȼ�ȡǰ�����ȡ����
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
     * ����ɢ����
     * ������
     * (x, y): ��ǰλ��
     * intensity: �����ع���ǿ�ȣ�ǿ����
     * iteration: ������������lightRadiusС
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
            // ����ǰ�������Χ����
            for (int nx = x - 1; nx < x + 2; nx++)
            {
                for (int ny = y - 1; ny < y + 2; ny++)
                {
                    if (nx != x || ny != y)
                    {
                        // ���ݾ�������¹���ǿ��
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(nx, ny));
                        float targetIntensity = Mathf.Pow(thresh, dist) * intensity;
                        if (worldTilesMap.GetPixel(nx, ny) != null)
                        {
                            if (worldTilesMap.GetPixel(nx, ny).r < targetIntensity) // ֻ�бȹⰵ�ĵط��ᱻ����
                                LightBlock(nx, ny, targetIntensity, iteration + 1);
                        }
                    }
                }
            }

            worldTilesMap.Apply();
        }
    }

    /*
     * �����긽�������Ĺ��¼����ɢ
     * �ƻ���������������
     */
    void RemoveLightSource(int x, int y)
    {
        // ���
        unlitBlocks.Clear();
        UnLightBlock(x, y, x, y);

        List<Vector2Int> toRelight = new List<Vector2Int>();

        // unlitBlocks�е�ÿһ�����꣬���ǵ��ھӣ�����
        // unlitblocks��Χ��������Լ���ĵ㣬�ͻ����toRelight
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

        // ��ɢ�������������������unlitBlocks
        foreach (Vector2Int source in toRelight)
        {
            LightBlock(source.x, source.y, worldTilesMap.GetPixel(source.x, source.y).r, 0);
        }

        worldTilesMap.Apply();
    }

    /*
     * ������
     * (x, y)��λ��
     * (ix, iy)��initial λ�� 
     * 
     * ��⺯�����������Ϊ���ģ���뾶�ڱ�ڲ���¼ΪunlitBlocks
     */
    void UnLightBlock(int x, int y, int ix, int iy)
    {
        if (Mathf.Abs(x - ix) >= lightRadius || Mathf.Abs(y - iy) >= lightRadius || unlitBlocks.Contains(new Vector2Int(x, y)))
            return;

        // ��Χ���ȵ������ĵ�,�Ƴ����ղ���¼
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
