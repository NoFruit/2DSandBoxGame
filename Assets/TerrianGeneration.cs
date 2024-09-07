using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrianGeneration : MonoBehaviour
{
    [Header("Lighting")]
    // Tile light part
    public Texture2D worldTilesMap;
    public Material lightShader;
    public float lightThreshold;
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

    private GameObject[] worldChunks;   // �����б�

    // ������˸�� private 
    public List<Vector2> worldTiles = new List<Vector2>();  // ���������б�������һά����indexλ�ô洢��������
    public List<GameObject> worldTilesObjects = new List<GameObject>();
    public List<TileClass> worldTilesClasses = new List<TileClass>();

    private BiomeClass curBiome;
    private Color[] biomeCols;


    private void Start()
    {
        // ���ձ�����ʼ��
        worldTilesMap = new Texture2D(worldSize, worldSize);
        worldTilesMap.filterMode = FilterMode.Bilinear;
        lightShader.SetTexture("_ShadowTex", worldTilesMap);

        for (int x = 0; x < worldSize; x++)
        {
            for(int y = 0; y < worldSize; y++)
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

        // TODO ��������ú���Ƶ������ֲ���������㷨��ø���һЩ����Ϊ���������ض�������
        DrawBiomeMap();
        DrawTextures();
        DrawCavesAndOres();

        // ��������
        CreateChunks();
        GenerateTerrain();

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
        for(int i = 0; i < worldChunks.Length; i++)
        {
            if (Vector2.Distance(new Vector2((i * chunkSize) + (chunkSize / 2), 0), new Vector2(player.transform.position.x, 0)) > Camera.main.orthographicSize * 6f)
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
        // �ı�curBiome��ֵ

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
        TileClass tileClass;
        for(int x = 0; x < worldSize; x++)
        {
            // �ڵ�ǰ��x���������θ߶ȣ�Perlin���������ڲ�ͬѭ�����ָ߶�ֵƽ�� 
            float height;

            for(int y = 0; y < worldSize; y++)
            {
                /* ���ݵ�ǰ�߶ȸı���η���
                 * ���ε�ÿһ�У�
                 *      ��һ��ݷ���
                 *      �ڶ���������
                 *      ������ʯͷ����
                 *          - ʯͷ�����У����ݿ�ʯ��ϡ�жȴ�С��������
                 * 
                 * ���ݲ�ͬ������Ⱥϵ�����ѡ���Ӧ�ķ��鼯
                 */
                // ��������Ⱥϵ��ɫ
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
                    else if(tileClass.wallVariant != null)
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
                        if (worldTiles.Contains(new Vector2(x, y)))
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
                            if (worldTiles.Contains(new Vector2(x, y)))
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
     * ������
     * (x, y)����
     * 
     * ����������λ�����ϰεض���һ����
     */
    public void GenerateTree(int treeHeight, int x, int y)
    {
        // ������������
        for (int i = 0; i < treeHeight; i++)
        {
            PlaceTile(tileAtlas.log, x, y + i, true);
        }

        // ���Ҷ��
        PlaceTile(tileAtlas.leaf, x, y + treeHeight, true);
        PlaceTile(tileAtlas.leaf, x, y + treeHeight + 1, true);
        PlaceTile(tileAtlas.leaf, x, y + treeHeight + 2, true);

        PlaceTile(tileAtlas.leaf, x - 1, y + treeHeight, true);
        PlaceTile(tileAtlas.leaf, x - 1, y + treeHeight + 1, true);

        PlaceTile(tileAtlas.leaf, x + 1, y + treeHeight, true);
        PlaceTile(tileAtlas.leaf, x + 1, y + treeHeight + 1, true);

        // TODO ������Ҷ�Ӵ�С���ɲ�ͬ��С����״
    }

    public void GenerateCactus(TileAtlas atlas, int treeHeight, int x, int y)
    {
        for (int i = 0; i < treeHeight; i++)
        {
            PlaceTile(atlas.log, x, y + i, true);
        }
    }

    /*
     * ����:
     * tile: ������з���
     * (x, y): ����
     * isNaturallyPlaced: ��Ȼ����
     * 
     * ������������з��ò���ʱ��
     * �������������з��ò�����
     * ͬʱ������������ǽ���������ݣ�������ǽ�����ƻ��ķ�֧
     * 
     * TODO ������ڱ���ӡ��ƻ���������ʡ�����Ҫ���¹�д
     */
    public void CheckTile(TileClass tile, int x, int y, bool isNaturallyPlaced)
    {
        if (x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            if (!worldTiles.Contains(new Vector2Int(x, y)))
            {
                RemoveLightSource(x, y);
                // ����Tile
                PlaceTile(tile, x, y, isNaturallyPlaced);
            }
            else
            {
                if (worldTilesClasses[worldTiles.IndexOf(new Vector2Int(x, y))].isBackground)
                {
                    RemoveLightSource(x, y);
                    // ����Tileǽ
                    RemoveTile(x, y);
                    PlaceTile(tile, x, y, isNaturallyPlaced);
                }
            }
        }
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
        bool backgroundElement = tile.isBackground;

        if (!worldTiles.Contains(new Vector2Int(x, y)) && x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            GameObject newTile = new GameObject();

            // ��������������ǰtile���õ���Ӧ��chunk
            int chunkCoord = Mathf.RoundToInt(Mathf.Round(x / chunkSize) * chunkSize);
            chunkCoord /= chunkSize;

            newTile.transform.parent = worldChunks[chunkCoord].transform;  // ���Ϊ��Ӧ������Ӷ���

            newTile.AddComponent<SpriteRenderer>(); // ���Sprite Renderer
            if (!backgroundElement)
            {
                newTile.AddComponent<BoxCollider2D>();
                newTile.GetComponent<BoxCollider2D>().size = Vector2.one;
                newTile.tag = "Ground";
            }

            int spriteIndex = Random.Range(0, tile.tileSprites.Length);
            newTile.GetComponent<SpriteRenderer>().sprite = tile.tileSprites[spriteIndex];   // �������sprite

            if (tile.isBackground)
                newTile.GetComponent<SpriteRenderer>().sortingOrder = -10;
            else
                newTile.GetComponent<SpriteRenderer>().sortingOrder = -5;

            if (tile.name.ToUpper().Contains("WALL"))
            {
                newTile.GetComponent<SpriteRenderer>().color = new Color(0.6f, 0.6f, 0.6f);
                worldTilesMap.SetPixel(x, y, Color.black);
            }
            else if (!tile.isBackground)
            {
                worldTilesMap.SetPixel(x, y, Color.black);
            }

            newTile.name = tile.tileSprites[0].name;
            newTile.transform.position = new Vector2(x + 0.5f, y + 0.5f);

            TileClass newTileClass = TileClass.CreateInstance(tile, isNaturallyPlaced);

            worldTiles.Add(newTile.transform.position - (Vector3.one * 0.5f));
            worldTilesObjects.Add(newTile);
            worldTilesClasses.Add(newTileClass);
        }
    }

    public void RemoveTile(int x, int y)
    {
        if (worldTiles.Contains(new Vector2Int(x, y)) && x >= 0 && x <= worldSize && y >= 0 && y <= worldSize)
        {
            TileClass currentTileClass = worldTilesClasses[worldTiles.IndexOf(new Vector2(x, y))];

            Destroy(worldTilesObjects[worldTiles.IndexOf(new Vector2(x, y))]);
            worldTilesMap.SetPixel(x, y, Color.white);

            // �����Ƿ���䷽�� ���뷽����书��
            if (currentTileClass.tileDrop)
            {
                GameObject newtileDrop = Instantiate(tileDrop, new Vector2(x, y + 0.5f), Quaternion.identity);
                newtileDrop.GetComponent<SpriteRenderer>().sprite = currentTileClass.tileDrop;
            }

            worldTilesObjects.RemoveAt(worldTiles.IndexOf(new Vector2(x, y)));
            worldTilesClasses.RemoveAt(worldTiles.IndexOf(new Vector2(x, y)));
            worldTiles.RemoveAt(worldTiles.IndexOf(new Vector2(x, y)));

            // ����Ȼ���ɷ��鱻�ƻ����б���ǽ�����·����ڵ�ǰ����
            if (currentTileClass.wallVariant != null)
            { 
                if (currentTileClass.naturallyPlaced)
                {
                    PlaceTile(currentTileClass.wallVariant, x, y, true);
                }
            
            }

            LightBlock(x, y, 1f, 0);
            worldTilesMap.Apply();
        }
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
                        float targetIntensity = Mathf.Pow(0.7f, dist) * intensity;
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
     * When place tile
     * remove the light there
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
                    if (worldTilesMap.GetPixel(nx, ny) != null) // if there have a light
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

        // ��ɢ�������������
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
     * ��⺯�����������Ϊ���ģ���뾶�ڱ�ڲ����õ�⺯��������Ⱦ
     */
    void UnLightBlock(int x, int y, int ix, int iy)
    {
        if (Mathf.Abs(x - ix) >= lightRadius || Mathf.Abs(y - iy) >= lightRadius || unlitBlocks.Contains(new Vector2Int(x, y)))
            return;
        
        for (int nx = x - 1; nx < x + 2; nx++)
        {
            for (int ny = y - 1; ny < y + 2; ny++)
            {
                if (nx != x || ny != y)
                {
                    if (worldTilesMap.GetPixel(nx, ny) != null)
                    {
                        if (worldTilesMap.GetPixel(nx, ny).r < worldTilesMap.GetPixel(x, y).r) // ��Χ���ȵ���
                            UnLightBlock(nx, ny, ix, iy);
                    }
                }
            }
        }

        worldTilesMap.SetPixel(x, y, Color.black);
        unlitBlocks.Add(new Vector2Int(x, y));
    }

}
