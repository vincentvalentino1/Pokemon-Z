using UnityEngine;
using UnityEditor;
using System.IO;

public class BattleArenaBuilder : EditorWindow
{
    public enum Biome { Grassland, RockyCanyon, SnowyField, ForestFloor }

    Biome _selectedBiome = Biome.Grassland;
    float _hillStrength = 0.4f;
    float _noiseScale = 0.06f;
    bool _addGrassDetail = true;

    const string ArenaFolder = "Assets/BatlleScene/Arena";
    const int HeightRes = 257;
    const int AlphaRes = 256;
    const int DetailRes = 256;
    const float TerrainWidth = 60f;
    const float TerrainLength = 60f;
    const float TerrainMaxHeight = 4f;

    static readonly Vector3 TerrainOffset = new Vector3(-20f, -2f, -10f);

    [MenuItem("Tools/Battle/Build Battle Arena")]
    static void ShowWindow()
    {
        var win = GetWindow<BattleArenaBuilder>("Battle Arena Builder");
        win.minSize = new Vector2(320, 220);
        win.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Battle Arena Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        _selectedBiome = (Biome)EditorGUILayout.EnumPopup("Biome", _selectedBiome);
        _hillStrength = EditorGUILayout.Slider("Hill Strength", _hillStrength, 0.05f, 1f);
        _noiseScale = EditorGUILayout.Slider("Noise Scale", _noiseScale, 0.02f, 0.15f);
        _addGrassDetail = EditorGUILayout.Toggle("Add Grass Detail", _addGrassDetail);

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Generate Arena", GUILayout.Height(36)))
            Generate();
    }

    void Generate()
    {
        EnsureFolder(ArenaFolder);

        RemoveExistingTerrain();

        TerrainData tData = new TerrainData();
        tData.heightmapResolution = HeightRes;
        tData.alphamapResolution = AlphaRes;
        tData.SetDetailResolution(DetailRes, 16);
        tData.size = new Vector3(TerrainWidth, TerrainMaxHeight, TerrainLength);

        string tdPath = $"{ArenaFolder}/BattleArena_{_selectedBiome}.asset";
        AssetDatabase.CreateAsset(tData, tdPath);

        ApplyHeightmap(tData);
        ApplyLayers(tData);
        PaintSplatmap(tData);

        if (_addGrassDetail)
            AddGrassDetail(tData);

        AssetDatabase.SaveAssets();

        GameObject terrainGO = Terrain.CreateTerrainGameObject(tData);
        terrainGO.name = "BattleArena";
        terrainGO.transform.position = TerrainOffset;

        Terrain terrain = terrainGO.GetComponent<Terrain>();
        terrain.materialTemplate = GetURPTerrainMaterial();
        terrain.drawInstanced = true;
        terrain.allowAutoConnect = false;
        terrain.detailObjectDensity = 0.8f;
        terrain.detailObjectDistance = 80f;

        Undo.RegisterCreatedObjectUndo(terrainGO, "Create Battle Arena");
        EditorUtility.SetDirty(tData);

        Debug.Log($"[BattleArenaBuilder] {_selectedBiome} arena created at {TerrainOffset}");
        EditorUtility.DisplayDialog("Done",
            $"{_selectedBiome} battle arena generated!\n\n" +
            $"Terrain: {TerrainWidth}x{TerrainLength} units\n" +
            $"Position: {TerrainOffset}\n\n" +
            "Save the scene (Cmd+S / Ctrl+S).", "OK");
    }

    // ════════════════════════════════════════════
    //  HEIGHTMAP
    // ════════════════════════════════════════════

    void ApplyHeightmap(TerrainData tData)
    {
        float[,] heights = new float[HeightRes, HeightRes];
        int seed = Random.Range(0, 10000);

        for (int y = 0; y < HeightRes; y++)
        {
            for (int x = 0; x < HeightRes; x++)
            {
                float nx = (float)x / HeightRes;
                float ny = (float)y / HeightRes;

                float h = 0f;

                h += Mathf.PerlinNoise((nx + seed) * _noiseScale * 100f,
                                        (ny + seed) * _noiseScale * 100f) * _hillStrength;

                h += Mathf.PerlinNoise((nx + seed) * _noiseScale * 200f,
                                        (ny + seed) * _noiseScale * 200f) * _hillStrength * 0.3f;

                h += Mathf.PerlinNoise((nx + seed) * _noiseScale * 400f,
                                        (ny + seed) * _noiseScale * 400f) * _hillStrength * 0.1f;

                float battleCX = (21f) / TerrainWidth;
                float battleCY = (17f) / TerrainLength;
                float distFromBattle = Vector2.Distance(new Vector2(nx, ny), new Vector2(battleCX, battleCY));

                float battleRadius = 0.2f;
                float battleFlatten = Mathf.Clamp01((distFromBattle - battleRadius) / 0.15f);
                battleFlatten = Mathf.SmoothStep(0f, 1f, battleFlatten);
                h *= battleFlatten;

                float edgeDist = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                float edgeFade = Mathf.Clamp01(1f - edgeDist * 1.8f);
                edgeFade = edgeFade * edgeFade;
                h *= edgeFade;

                h = Mathf.Clamp(h, 0f, 0.8f);

                if (_selectedBiome == Biome.RockyCanyon)
                    h = Mathf.Pow(h, 0.7f) * 1.3f;
                else if (_selectedBiome == Biome.SnowyField)
                    h *= 0.6f;
                else if (_selectedBiome == Biome.ForestFloor)
                    h *= 0.5f;

                heights[y, x] = h * 0.25f + 0.2f;
            }
        }

        tData.SetHeights(0, 0, heights);
    }

    // ════════════════════════════════════════════
    //  TERRAIN LAYERS
    // ════════════════════════════════════════════

    void ApplyLayers(TerrainData tData)
    {
        BiomeColors bc = GetBiomeColors(_selectedBiome);

        TerrainLayer baseLayer = CreateLayer($"Base_{_selectedBiome}", bc.baseColor, 4f);
        TerrainLayer dirtLayer = CreateLayer($"Dirt_{_selectedBiome}", bc.dirtColor, 6f);
        TerrainLayer rockLayer = CreateLayer($"Rock_{_selectedBiome}", bc.rockColor, 8f);
        TerrainLayer accentLayer = CreateLayer($"Accent_{_selectedBiome}", bc.accentColor, 5f);

        tData.terrainLayers = new TerrainLayer[] { baseLayer, dirtLayer, rockLayer, accentLayer };
    }

    TerrainLayer CreateLayer(string name, Color color, float tileSize)
    {
        string layerPath = $"{ArenaFolder}/{name}.terrainlayer";
        TerrainLayer existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing != null)
        {
            existing.diffuseTexture = CreateColorTexture(name, color);
            existing.tileSize = new Vector2(tileSize, tileSize);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = CreateColorTexture(name, color);
        layer.tileSize = new Vector2(tileSize, tileSize);
        layer.tileOffset = Vector2.zero;

        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    Texture2D CreateColorTexture(string name, Color color)
    {
        string texPath = $"{ArenaFolder}/Tex_{name}.asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existing != null)
        {
            existing.SetPixels(new Color[] {
                color, color * 0.95f,
                color * 0.97f, color * 0.92f
            });
            existing.Apply();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(new Color[] {
            color, color * 0.95f,
            color * 0.97f, color * 0.92f
        });
        tex.Apply();

        AssetDatabase.CreateAsset(tex, texPath);
        return tex;
    }

    // ════════════════════════════════════════════
    //  SPLATMAP PAINTING
    // ════════════════════════════════════════════

    void PaintSplatmap(TerrainData tData)
    {
        int w = tData.alphamapWidth;
        int h = tData.alphamapHeight;
        float[,,] splat = new float[h, w, 4];

        float[,] heights = tData.GetHeights(0, 0, HeightRes, HeightRes);

        int seed = Random.Range(0, 10000);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / w;
                float ny = (float)y / h;

                int hx = Mathf.FloorToInt(nx * (HeightRes - 1));
                int hy = Mathf.FloorToInt(ny * (HeightRes - 1));
                hx = Mathf.Clamp(hx, 1, HeightRes - 2);
                hy = Mathf.Clamp(hy, 1, HeightRes - 2);

                float heightVal = heights[hy, hx];

                float dx = heights[hy, hx + 1] - heights[hy, hx - 1];
                float dy = heights[hy + 1, hx] - heights[hy - 1, hx];
                float slope = Mathf.Sqrt(dx * dx + dy * dy) * HeightRes;

                float baseW = 1f;
                float dirtW = 0f;
                float rockW = 0f;
                float accentW = 0f;

                dirtW = Mathf.Clamp01(slope * 3f - 0.3f);

                rockW = Mathf.Clamp01(slope * 4f - 1.2f);

                float heightFactor = Mathf.Clamp01((heightVal - 0.3f) * 3f);
                rockW = Mathf.Max(rockW, heightFactor * 0.6f);

                float accentNoise = Mathf.PerlinNoise(
                    (nx + seed) * 15f,
                    (ny + seed) * 15f);
                if (accentNoise > 0.6f)
                    accentW = (accentNoise - 0.6f) * 2.5f;

                baseW = Mathf.Max(0f, 1f - dirtW - rockW - accentW);

                if (_selectedBiome == Biome.RockyCanyon)
                {
                    rockW *= 1.5f;
                    baseW *= 0.7f;
                }
                else if (_selectedBiome == Biome.ForestFloor)
                {
                    dirtW *= 1.3f;
                    accentW *= 1.5f;
                }

                float total = baseW + dirtW + rockW + accentW;
                if (total > 0.001f)
                {
                    splat[y, x, 0] = baseW / total;
                    splat[y, x, 1] = dirtW / total;
                    splat[y, x, 2] = rockW / total;
                    splat[y, x, 3] = accentW / total;
                }
                else
                {
                    splat[y, x, 0] = 1f;
                }
            }
        }

        tData.SetAlphamaps(0, 0, splat);
    }

    // ════════════════════════════════════════════
    //  GRASS DETAIL
    // ════════════════════════════════════════════

    void AddGrassDetail(TerrainData tData)
    {
        BiomeColors bc = GetBiomeColors(_selectedBiome);

        DetailPrototype grass = new DetailPrototype();
        grass.prototypeTexture = null;
        grass.renderMode = DetailRenderMode.GrassBillboard;
        grass.healthyColor = bc.grassHealthy;
        grass.dryColor = bc.grassDry;
        grass.minWidth = 0.4f;
        grass.maxWidth = 0.8f;
        grass.minHeight = 0.3f;
        grass.maxHeight = 0.6f;
        grass.noiseSpread = 0.3f;

        tData.detailPrototypes = new DetailPrototype[] { grass };

        int[,] detailMap = new int[DetailRes, DetailRes];
        float[,,] splat = tData.GetAlphamaps(0, 0, AlphaRes, AlphaRes);

        for (int y = 0; y < DetailRes; y++)
        {
            for (int x = 0; x < DetailRes; x++)
            {
                int sx = Mathf.FloorToInt((float)x / DetailRes * (AlphaRes - 1));
                int sy = Mathf.FloorToInt((float)y / DetailRes * (AlphaRes - 1));
                sx = Mathf.Clamp(sx, 0, AlphaRes - 1);
                sy = Mathf.Clamp(sy, 0, AlphaRes - 1);

                float grassChance = splat[sy, sx, 0] * 0.8f + splat[sy, sx, 3] * 0.4f;

                if (_selectedBiome == Biome.SnowyField)
                    grassChance *= 0.2f;
                else if (_selectedBiome == Biome.RockyCanyon)
                    grassChance *= 0.3f;
                else if (_selectedBiome == Biome.ForestFloor)
                    grassChance *= 1.3f;

                if (Random.value < grassChance)
                    detailMap[y, x] = Random.Range(1, 4);
            }
        }

        tData.SetDetailLayer(0, 0, 0, detailMap);
    }

    // ════════════════════════════════════════════
    //  BIOME COLOR DEFINITIONS
    // ════════════════════════════════════════════

    struct BiomeColors
    {
        public Color baseColor, dirtColor, rockColor, accentColor;
        public Color grassHealthy, grassDry;
    }

    static BiomeColors GetBiomeColors(Biome biome)
    {
        switch (biome)
        {
            case Biome.Grassland:
                return new BiomeColors {
                    baseColor    = new Color(0.30f, 0.55f, 0.18f),
                    dirtColor    = new Color(0.45f, 0.35f, 0.20f),
                    rockColor    = new Color(0.42f, 0.40f, 0.38f),
                    accentColor  = new Color(0.35f, 0.62f, 0.22f),
                    grassHealthy = new Color(0.25f, 0.60f, 0.15f),
                    grassDry     = new Color(0.50f, 0.55f, 0.20f)
                };

            case Biome.RockyCanyon:
                return new BiomeColors {
                    baseColor    = new Color(0.60f, 0.48f, 0.30f),
                    dirtColor    = new Color(0.55f, 0.38f, 0.22f),
                    rockColor    = new Color(0.35f, 0.32f, 0.28f),
                    accentColor  = new Color(0.70f, 0.55f, 0.35f),
                    grassHealthy = new Color(0.45f, 0.50f, 0.20f),
                    grassDry     = new Color(0.55f, 0.48f, 0.25f)
                };

            case Biome.SnowyField:
                return new BiomeColors {
                    baseColor    = new Color(0.88f, 0.90f, 0.95f),
                    dirtColor    = new Color(0.55f, 0.60f, 0.68f),
                    rockColor    = new Color(0.30f, 0.32f, 0.35f),
                    accentColor  = new Color(0.75f, 0.82f, 0.90f),
                    grassHealthy = new Color(0.35f, 0.50f, 0.30f),
                    grassDry     = new Color(0.55f, 0.55f, 0.45f)
                };

            case Biome.ForestFloor:
                return new BiomeColors {
                    baseColor    = new Color(0.20f, 0.40f, 0.15f),
                    dirtColor    = new Color(0.35f, 0.25f, 0.15f),
                    rockColor    = new Color(0.28f, 0.35f, 0.25f),
                    accentColor  = new Color(0.25f, 0.48f, 0.18f),
                    grassHealthy = new Color(0.18f, 0.52f, 0.12f),
                    grassDry     = new Color(0.30f, 0.42f, 0.18f)
                };

            default:
                return GetBiomeColors(Biome.Grassland);
        }
    }

    // ════════════════════════════════════════════
    //  UTILITIES
    // ════════════════════════════════════════════

    static void RemoveExistingTerrain()
    {
        foreach (var t in Object.FindObjectsOfType<Terrain>())
        {
            if (t.gameObject.name == "BattleArena")
                Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    static Material GetURPTerrainMaterial()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material TerrainLit");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.shader != null && mat.shader.name.Contains("Terrain"))
                return mat;
        }

        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (terrainShader == null)
            terrainShader = Shader.Find("Nature/Terrain/Standard");

        if (terrainShader != null)
        {
            Material mat = new Material(terrainShader);
            mat.name = "BattleArena_TerrainMat";
            string matPath = $"{ArenaFolder}/BattleArena_TerrainMat.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
