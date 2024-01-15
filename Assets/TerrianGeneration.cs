using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrianGeneration : MonoBehaviour
{
    /*
     * ���������ز�
     */
    [Header("Tile Sprites")]
    public float seed;
    public TileAtlas tileAtlas;

    /*
     * ����Ⱥϵ��
     */
    public BiomeClass[] biomes;



    /*
     * [Trees]��ľ����
     * treeChance: ��ľ���ָ��ʣ�������������;
     * minTreeHeight: ��С���ߣ�
     * maxTreeHeight: ������ߣ�
     * 
     *  [Header("Biomes")]����Ⱥϵ
     *  public Color grassland;
     *  public Color desert;
     *  public Color snow;
     *  public Color forest;
     *  ��ɫ����ͬ����Ⱥϵ
     *  public Texture2D biomeMap: ����Ⱥϵ��������
     * 
     * [Generation Settings]
     * chunkSize: �����С��
     * worldSize: ��ͼ��С��
     * generateCaves: ���Ե����Ƿ�������ȷ���ų���Ѩ���ţ�
     * dirtLayerHight: ����߶ȣ�
     * surfaceValue: ���γ��ֵ���ֵ���غ����θ��Ӷȣ��з������������
     * heightMultiplier: ���θ߶�����ϵ�������εĳ�����
     * heightAddition: ���θ߶Ȼ������������εļ�����
     * 
     * [Noise Settings]�����������
     * terrainFreq: ���ΰ�͹�̶ȣ�
     * caveFreq: ��Ѩ���ָ��ʣ��ֲ�������ʹ�������Ƶ�ʸߣ�
     * seed: �������������ӣ�
     * noiseTexture: ��������
     * 
     * [Ore Settings]��ʯ����
     * һ����ʯ����б�
     * 
     * worldChunks��ά�������б�
     * worldTiles: ��¼��ͼĳ�����Ƿ��з��飻
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

        // �����������
        seed = Random.Range(-10000, 10000);

        // TODO ��������ú���Ƶ������ֲ���������㷨��ø���һЩ����Ϊ���������ض�������

        DrawTextures();
        DrawCavesAndOres();

        // ��������
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
        // ����Ⱥϵ��������
        biomeMap = new Texture2D(worldSize, worldSize);
        DrawBiomeTexture();

        for (int i = 0; i < biomes.Length; i++)
        {

            // ��Ѩ��������
            GenerateNoiseTexture(biomes[i].caveFreq, biomes[i].surfaceValue, ref biomes[i].caveNoiseTexture);

            // ������������
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
                // ����ɫ����ʹ���ݶ�����ʾ��ɫ
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
        // �ı�curBiome��ֵ

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
            // �ڵ�ǰ��x���������θ߶ȣ�Perlin���������ڲ�ͬѭ�����ָ߶�ֵƽ�� 
            float height = Mathf.PerlinNoise((x + seed) * curBiome.terrainFreq, seed * curBiome.terrainFreq) * curBiome.heightMultiplier + heightAddition;

            for(int y = 0; y < height; y++)
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

                // ������������ȷ����ǰ������û�ж�Ѩ
                // �����÷���
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
                            // ������
                            GenerateTree(Random.Range(curBiome.minTreeHeight, curBiome.maxTreeHeight), x, y + 1);
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
     * ������
     * frequency���ض��������
     * limit: ����ֵ
     * noiseTexture���ض��������
     * 
     * �������ɶ�Ӧ����ĵ��ε��������������Ǿۼ�
     * �����κγ�������ֵ���������أ���������Ϊ����
     * �����д��ײ����ǵ��λ����ɵ�λ��
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
            PlaceTile(tileAtlas.log.tileSprites, x, y + i);
        }

        // ���Ҷ��
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight);
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight + 1);
        PlaceTile(tileAtlas.leaf.tileSprites, x, y + treeHeight + 2);

        PlaceTile(tileAtlas.leaf.tileSprites, x - 1, y + treeHeight);
        PlaceTile(tileAtlas.leaf.tileSprites, x - 1, y + treeHeight + 1);

        PlaceTile(tileAtlas.leaf.tileSprites, x + 1, y + treeHeight);
        PlaceTile(tileAtlas.leaf.tileSprites, x + 1, y + treeHeight + 1);

        // TODO ������Ҷ�Ӵ�С���ɲ�ͬ��С����״
    }

    /*
     * ����
     * tileSprite: ��Ӧ����Sprite��
     * (x, y)����
     * 
     * ���ض�λ�����ɶ�Ӧ�ķ��飬���ѷ��������ʵ�������
     * �����б��м�¼�������꣬��¼��λ���з���
     */
    public void PlaceTile(Sprite[] tileSprites, int x, int y)
    {
        if (!worldTiles.Contains(new Vector2Int(x, y)))
        {
            GameObject newTile = new GameObject();

            // ��������������ǰtile���õ���Ӧ��chunk
            int chunkCoord = Mathf.RoundToInt(Mathf.Round(x / chunkSize) * chunkSize);
            chunkCoord /= chunkSize;

            newTile.transform.parent = worldChunks[chunkCoord].transform;  // ���Ϊ��Ӧ������Ӷ���

            newTile.AddComponent<SpriteRenderer>(); // ���Sprite Renderer

            int spriteIndex = Random.Range(0, tileSprites.Length);
            newTile.GetComponent<SpriteRenderer>().sprite = tileSprites[spriteIndex];   // �������sprite

            newTile.name = tileSprites[0].name;
            newTile.transform.position = new Vector2(x + 0.5f, y + 0.5f);

            worldTiles.Add(newTile.transform.position - (Vector3.one * 0.5f));
        }
    }
}
