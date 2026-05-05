using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CaveSceneBuilder
{
    const string TargetSceneName = "CaveScene";
    const string GeneratedRootName = "Generated Cave Interior";
    const string SystemsRootName = "Generated Cave Systems";
    const string AssetFolder = "Assets/Scenes/CaveGenerated";
    const string FallbackTerrainDataPath = AssetFolder + "/GeneratedCaveTerrainData.asset";

    static readonly Vector3 TerrainSize = new Vector3(180f, 26f, 180f);
    const float CeilingHeightBase = 15.5f;
    const float EntranceArchHeight = 12.8f;
    static readonly Rect MainChamber = new Rect(42f, 46f, 96f, 88f);
    static readonly Rect EntranceRoom = new Rect(72f, 18f, 36f, 24f);
    static readonly Rect InnerRoom = new Rect(64f, 122f, 52f, 24f);

    [MenuItem("Tools/World/Build Cave Interior Scene")]
    static void BuildCaveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog("Cave Builder", "Open CaveScene first, then run this tool again.", "OK");
            return;
        }

        string currentSceneName = Path.GetFileNameWithoutExtension(activeScene.path);
        if (!string.Equals(currentSceneName, TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            bool continueAnyway = EditorUtility.DisplayDialog(
                "Cave Builder",
                $"This tool is meant for {TargetSceneName}. The active scene is {currentSceneName}.\n\nBuild anyway?",
                "Build Anyway",
                "Cancel");
            if (!continueAnyway)
                return;
        }

        EnsureFolder(AssetFolder);
        CleanupGeneratedObjects();

        Terrain terrain = FindOrCreateTerrain();
        ConfigureTerrain(terrain);

        GameObject generatedRoot = new GameObject(GeneratedRootName);
        BuildCaveGeometry(generatedRoot.transform, terrain);
        BuildCaveLighting(generatedRoot.transform, terrain);
        BuildWorldSystems(terrain);
        ConfigureRenderSettings();
        ConfigureCamera();
        ConfigureDirectionalLight();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = generatedRoot;

        EditorUtility.DisplayDialog(
            "Cave Scene Ready",
            "Generated a playable cave interior with cave walls, darker lighting, floor painting, and a player start.\n\nRe-run the tool any time to refresh the generated cave content.",
            "OK");
    }

    static void CleanupGeneratedObjects()
    {
        DestroyImmediateIfFound(GeneratedRootName);
        DestroyImmediateIfFound(SystemsRootName);
    }

    static Terrain FindOrCreateTerrain()
    {
        Terrain terrain = Object.FindObjectOfType<Terrain>();
        if (terrain != null)
            return terrain;

        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(FallbackTerrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, FallbackTerrainDataPath);
        }

        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = "Terrain";
        terrainObject.transform.position = Vector3.zero;
        return terrainObject.GetComponent<Terrain>();
    }

    static void ConfigureTerrain(Terrain terrain)
    {
        if (terrain == null)
            return;

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
            return;

        terrainData.heightmapResolution = 257;
        terrainData.alphamapResolution = 256;
        terrainData.baseMapResolution = 1024;
        terrainData.size = TerrainSize;

        TerrainLayer dirtLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Terrain/Dirt.terrainlayer");
        TerrainLayer rockLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Terrain/Rock.terrainlayer");
        TerrainLayer canyonDirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/BattleScene/Arena/Dirt_RockyCanyon.terrainlayer");

        if (dirtLayer != null && rockLayer != null && canyonDirt != null)
            terrainData.terrainLayers = new[] { dirtLayer, rockLayer, canyonDirt };

        terrainData.SetHeights(0, 0, BuildHeights(terrainData));
        if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length >= 3)
            terrainData.SetAlphamaps(0, 0, BuildAlphamaps(terrainData));

        terrain.drawInstanced = true;
        terrain.detailObjectDistance = 30f;
        terrain.detailObjectDensity = 0f;
        terrain.treeDistance = 50f;
        terrain.basemapDistance = 1000f;

        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null)
            collider.terrainData = terrainData;
    }

    static float[,] BuildHeights(TerrainData terrainData)
    {
        int resolution = terrainData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = x / (float)(resolution - 1) * TerrainSize.x;
                float worldZ = z / (float)(resolution - 1) * TerrainSize.z;

                float edgeDistance = Mathf.Min(Mathf.Min(worldX, TerrainSize.x - worldX), Mathf.Min(worldZ, TerrainSize.z - worldZ));
                float outerWall = Mathf.Clamp01((24f - edgeDistance) / 24f);
                float chamberBlend = Mathf.Max(GetRectMask(worldX, worldZ, MainChamber, 20f), GetRectMask(worldX, worldZ, EntranceRoom, 10f));
                float innerBlend = GetRectMask(worldX, worldZ, InnerRoom, 10f);
                float pathBlend = Mathf.Clamp01(GetPathMask(worldX, worldZ) * 1.15f);

                float noiseLarge = Mathf.PerlinNoise(worldX * 0.032f, worldZ * 0.032f) * 0.022f;
                float noiseSmall = Mathf.PerlinNoise(worldX * 0.12f + 17f, worldZ * 0.12f + 17f) * 0.005f;
                float baseHeight = 0.05f + noiseLarge + noiseSmall + outerWall * outerWall * 0.16f;

                float chamberFlat = Mathf.Lerp(baseHeight, 0.038f, chamberBlend);
                chamberFlat = Mathf.Lerp(chamberFlat, 0.034f, innerBlend);
                heights[z, x] = Mathf.Lerp(chamberFlat, 0.03f, pathBlend);
            }
        }

        return heights;
    }

    static float[,,] BuildAlphamaps(TerrainData terrainData)
    {
        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        float[,,] maps = new float[height, width, 3];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float worldX = x / (float)(width - 1) * TerrainSize.x;
                float worldZ = z / (float)(height - 1) * TerrainSize.z;

                float dirt = 0.55f;
                float rock = 0.35f;
                float accent = 0.1f;

                float pathMask = GetPathMask(worldX, worldZ);
                float chamberMask = Mathf.Max(GetRectMask(worldX, worldZ, MainChamber, 18f), GetRectMask(worldX, worldZ, EntranceRoom, 10f));
                float innerMask = GetRectMask(worldX, worldZ, InnerRoom, 10f);
                float edgeDistance = Mathf.Min(Mathf.Min(worldX, TerrainSize.x - worldX), Mathf.Min(worldZ, TerrainSize.z - worldZ));
                float wallMask = Mathf.Clamp01((20f - edgeDistance) / 20f);

                dirt = Mathf.Lerp(dirt, 0.8f, pathMask);
                accent = Mathf.Lerp(accent, 0.22f, chamberMask * 0.5f + innerMask * 0.35f);
                rock = Mathf.Lerp(rock, 0.78f, wallMask);
                dirt *= 1f - wallMask * 0.55f;

                float sum = dirt + rock + accent;
                maps[z, x, 0] = dirt / sum;
                maps[z, x, 1] = rock / sum;
                maps[z, x, 2] = accent / sum;
            }
        }

        return maps;
    }

    static void BuildCaveGeometry(Transform root, Terrain terrain)
    {
        Transform shellRoot = new GameObject("Cave Shell").transform;
        shellRoot.SetParent(root);
        Material fallbackRockMaterial = GetOrCreateRockMaterial("Cave_RockFallback.mat", new Color(0.39f, 0.40f, 0.44f));

        GameObject[] wallPrefabs =
        {
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 1.prefab"),
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 3.prefab"),
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 5.prefab"),
        };

        GameObject[] chunkPrefabs =
        {
            LoadPrefab("Assets/3rd Party/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_03.prefab"),
            LoadPrefab("Assets/3rd Party/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_07.prefab"),
            LoadPrefab("Assets/3rd Party/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_10.prefab"),
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 4.prefab"),
        };

        GameObject[] tinyPrefabs =
        {
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 1.prefab"),
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 4.prefab"),
            LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 5.prefab"),
        };

        CreateShellRing(shellRoot, terrain, wallPrefabs, fallbackRockMaterial);
        CreateCeilingChunks(shellRoot, terrain, chunkPrefabs, fallbackRockMaterial);
        CreateInnerBoulders(shellRoot, terrain, chunkPrefabs, tinyPrefabs, fallbackRockMaterial);
        CreateEntranceArch(shellRoot, terrain, chunkPrefabs, fallbackRockMaterial);
        CreateCrystalClusters(shellRoot, terrain);
        CreateMushroomPatches(shellRoot, terrain);
        CreateSceneBounds(shellRoot);
    }

    static void CreateShellRing(Transform parent, Terrain terrain, GameObject[] wallPrefabs, Material fallbackRockMaterial)
    {
        for (int i = 0; i < 22; i++)
        {
            float t = i / 22f;
            Vector3 north = new Vector3(Mathf.Lerp(18f, 162f, t), 0f, 154f + Mathf.Sin(t * Mathf.PI * 4f) * 2f);
            Vector3 south = new Vector3(Mathf.Lerp(20f, 160f, t), 0f, 24f + Mathf.Cos(t * Mathf.PI * 4f) * 2f);
            PlaceRock(parent, terrain, wallPrefabs, north, new Vector3(8f, 10f, 7f), 200 + i, fallbackRockMaterial: fallbackRockMaterial);
            PlaceRock(parent, terrain, wallPrefabs, south, new Vector3(8f, 9f, 7f), 260 + i, fallbackRockMaterial: fallbackRockMaterial);
        }

        for (int i = 0; i < 16; i++)
        {
            float t = i / 16f;
            Vector3 west = new Vector3(18f + Mathf.Sin(t * Mathf.PI * 4f) * 2f, 0f, Mathf.Lerp(34f, 144f, t));
            Vector3 east = new Vector3(162f + Mathf.Cos(t * Mathf.PI * 4f) * 2f, 0f, Mathf.Lerp(34f, 144f, t));
            PlaceRock(parent, terrain, wallPrefabs, west, new Vector3(7f, 9f, 7f), 320 + i, fallbackRockMaterial: fallbackRockMaterial);
            PlaceRock(parent, terrain, wallPrefabs, east, new Vector3(7f, 9f, 7f), 380 + i, fallbackRockMaterial: fallbackRockMaterial);
        }
    }

    static void CreateCeilingChunks(Transform parent, Terrain terrain, GameObject[] chunkPrefabs, Material fallbackRockMaterial)
    {
        Vector3[] positions =
        {
            new Vector3(58f, 0f, 58f),
            new Vector3(122f, 0f, 60f),
            new Vector3(52f, 0f, 92f),
            new Vector3(128f, 0f, 96f),
            new Vector3(60f, 0f, 132f),
            new Vector3(120f, 0f, 136f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 pos = positions[i];
            float y = SampleTerrain(terrain, pos.x, pos.z) + CeilingHeightBase + (i % 2) * 1.4f;
            PlaceRock(parent, terrain, chunkPrefabs, new Vector3(pos.x, y, pos.z), new Vector3(6.8f, 2.4f, 6.8f), 500 + i, useTerrainY: false, fallbackRockMaterial: fallbackRockMaterial);
        }
    }

    static void CreateInnerBoulders(Transform parent, Terrain terrain, GameObject[] chunkPrefabs, GameObject[] tinyPrefabs, Material fallbackRockMaterial)
    {
        Vector3[] boulders =
        {
            new Vector3(54f, 0f, 60f),
            new Vector3(128f, 0f, 62f),
            new Vector3(56f, 0f, 110f),
            new Vector3(122f, 0f, 110f),
            new Vector3(90f, 0f, 138f),
            new Vector3(74f, 0f, 95f),
            new Vector3(108f, 0f, 94f)
        };

        for (int i = 0; i < boulders.Length; i++)
            PlaceRock(parent, terrain, chunkPrefabs, boulders[i], new Vector3(3.2f, 2.8f, 3.2f), 620 + i, fallbackRockMaterial: fallbackRockMaterial);

        Vector3[] pebbles =
        {
            new Vector3(84f, 0f, 46f),
            new Vector3(97f, 0f, 48f),
            new Vector3(68f, 0f, 73f),
            new Vector3(111f, 0f, 78f),
            new Vector3(78f, 0f, 126f),
            new Vector3(104f, 0f, 126f),
        };

        for (int i = 0; i < pebbles.Length; i++)
            PlaceRock(parent, terrain, tinyPrefabs, pebbles[i], new Vector3(1.4f, 1.1f, 1.4f), 700 + i, fallbackRockMaterial: fallbackRockMaterial);
    }

    static void CreateEntranceArch(Transform parent, Terrain terrain, GameObject[] chunkPrefabs, Material fallbackRockMaterial)
    {
        // Keep the cave entrance readable, but lift the arch high enough that it does not cut
        // through the player's camera or block the usable work/play space.
        PlaceRock(parent, terrain, chunkPrefabs, new Vector3(62f, 0f, 24f), new Vector3(4.6f, 7.6f, 4f), 760, fallbackRockMaterial: fallbackRockMaterial);
        PlaceRock(parent, terrain, chunkPrefabs, new Vector3(118f, 0f, 24f), new Vector3(4.6f, 7.6f, 4f), 761, fallbackRockMaterial: fallbackRockMaterial);
        PlaceRock(parent, terrain, chunkPrefabs, new Vector3(90f, SampleTerrain(terrain, 90f, 24f) + EntranceArchHeight, 24f), new Vector3(7.2f, 2.2f, 3.4f), 762, useTerrainY: false, fallbackRockMaterial: fallbackRockMaterial);
    }

    static void CreateCrystalClusters(Transform parent, Terrain terrain)
    {
        Transform crystalRoot = new GameObject("Crystal Clusters").transform;
        crystalRoot.SetParent(parent);

        Material crystalMaterial = GetOrCreateEmissiveMaterial("Cave_CrystalGlow.mat", new Color(0.24f, 0.86f, 1f), 2.6f);
        Vector3[] points =
        {
            new Vector3(60f, 0f, 54f),
            new Vector3(120f, 0f, 56f),
            new Vector3(52f, 0f, 122f),
            new Vector3(126f, 0f, 128f),
            new Vector3(92f, 0f, 144f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            Transform cluster = new GameObject("Crystal Cluster").transform;
            cluster.SetParent(crystalRoot);
            float y = SampleTerrain(terrain, points[i].x, points[i].z);
            cluster.position = new Vector3(points[i].x, y, points[i].z);

            for (int j = 0; j < 3; j++)
            {
                float xOffset = (j - 1) * 0.65f;
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shard.name = "Shard";
                shard.transform.SetParent(cluster);
                shard.transform.localPosition = new Vector3(xOffset, 0.8f + j * 0.18f, 0.2f * j);
                shard.transform.localRotation = Quaternion.Euler(8f + j * 6f, j * 25f, j * 9f);
                shard.transform.localScale = new Vector3(0.22f, 0.85f + j * 0.28f, 0.22f);
                Renderer renderer = shard.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = crystalMaterial;
            }
        }
    }

    static void CreateMushroomPatches(Transform parent, Terrain terrain)
    {
        GameObject mushroomPrefab = LoadPrefab("Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Mushroom/Mushrooms Patch.prefab");
        if (mushroomPrefab == null)
            return;

        Transform mushroomRoot = new GameObject("Mushrooms").transform;
        mushroomRoot.SetParent(parent);

        Vector3[] patches =
        {
            new Vector3(73f, 0f, 58f),
            new Vector3(106f, 0f, 64f),
            new Vector3(70f, 0f, 119f),
            new Vector3(111f, 0f, 117f),
        };

        for (int i = 0; i < patches.Length; i++)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(mushroomPrefab) as GameObject;
            if (instance == null)
                continue;

            float y = SampleTerrain(terrain, patches[i].x, patches[i].z);
            instance.transform.SetParent(mushroomRoot);
            instance.transform.position = new Vector3(patches[i].x, y, patches[i].z);
            instance.transform.rotation = Quaternion.Euler(0f, i * 52f, 0f);
            instance.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.5f, (i + 1) / 4f);
        }
    }

    static void BuildCaveLighting(Transform root, Terrain terrain)
    {
        Transform lightsRoot = new GameObject("Cave Lights").transform;
        lightsRoot.SetParent(root);

        CreatePointLight(lightsRoot, new Vector3(90f, SampleTerrain(terrain, 90f, 28f) + 6.5f, 28f), new Color(1f, 0.86f, 0.62f), 3f, 28f, "Entrance Glow");
        CreatePointLight(lightsRoot, new Vector3(90f, SampleTerrain(terrain, 90f, 82f) + 5.8f, 82f), new Color(0.58f, 0.92f, 1f), 2.3f, 22f, "Chamber Glow");
        CreatePointLight(lightsRoot, new Vector3(92f, SampleTerrain(terrain, 92f, 138f) + 5f, 138f), new Color(0.42f, 0.78f, 1f), 2f, 18f, "Deep Glow");
    }

    static void BuildWorldSystems(Terrain terrain)
    {
        GameObject systemsRoot = new GameObject(SystemsRootName);

        GameObject start = new GameObject("Player Start");
        start.transform.SetParent(systemsRoot.transform);
        float startY = SampleTerrain(terrain, 90f, 28f);
        start.transform.position = new Vector3(90f, startY + 1f, 28f);
        start.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        OpenWorldPlayerStart startMarker = start.AddComponent<OpenWorldPlayerStart>();
        startMarker.SpawnPointId = "cave_entry";
        startMarker.UseAsDefaultSpawn = true;

        GameObject sceneControllerObject = new GameObject("Open World Scene");
        sceneControllerObject.transform.SetParent(systemsRoot.transform);
        OpenWorldSceneController sceneController = sceneControllerObject.AddComponent<OpenWorldSceneController>();
        sceneController.SceneId = TargetSceneName;
        sceneController.SceneType = OpenWorldSceneType.Cave;
        sceneController.BattleSceneName = "BattleScene";
        sceneController.DefaultSpawnPointId = startMarker.SpawnPointId;
        sceneController.FallbackSpawnPosition = start.transform.position;
        sceneController.StarterSpeciesOverride = AssetDatabase.LoadAssetAtPath<PokemonData>("Assets/BattleScene/Pikachu.asset");
        sceneController.StarterLevelOverride = 5;
        sceneController.TrainerNameOverride = "Player";
        sceneController.StartingPokeBallsOverride = 12;
    }

    static void ConfigureRenderSettings()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.11f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 0f;
        RenderSettings.fogEndDistance = 85f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.12f, 0.14f, 1f);
        RenderSettings.subtractiveShadowColor = new Color(0.06f, 0.08f, 0.12f, 1f);
    }

    static void ConfigureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        OpenWorldMouseLook look = cam.GetComponent<OpenWorldMouseLook>();
        if (look == null)
            look = cam.gameObject.AddComponent<OpenWorldMouseLook>();

        look.ThirdPerson = true;
        look.OrbitDistance = 6.8f;
        look.OrbitVerticalLift = 2.4f;
        look.LookAtHeight = 1.35f;
        look.MouseSensitivity = 1.7f;
        look.MinPitch = 15f;
        look.MaxPitch = 44f;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 1f);
        cam.transform.position = new Vector3(90f, 8.2f, 20f);
        cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
    }

    static void ConfigureDirectionalLight()
    {
        Light light = Object.FindObjectOfType<Light>();
        if (light == null || light.type != LightType.Directional)
            return;

        light.color = new Color(0.32f, 0.38f, 0.46f, 1f);
        light.intensity = 0.22f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(65f, -18f, 0f);
    }

    static void CreateSceneBounds(Transform parent)
    {
        Transform boundsRoot = new GameObject("Scene Bounds").transform;
        boundsRoot.SetParent(parent);

        CreateBoundary(boundsRoot, new Vector3(90f, 4f, -2f), new Vector3(184f, 8f, 4f));
        CreateBoundary(boundsRoot, new Vector3(90f, 4f, 182f), new Vector3(184f, 8f, 4f));
        CreateBoundary(boundsRoot, new Vector3(-2f, 4f, 90f), new Vector3(4f, 8f, 184f));
        CreateBoundary(boundsRoot, new Vector3(182f, 4f, 90f), new Vector3(4f, 8f, 184f));
    }

    static void CreateBoundary(Transform parent, Vector3 position, Vector3 size)
    {
        GameObject go = new GameObject("Invisible Boundary");
        go.transform.SetParent(parent);
        go.transform.position = position;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = size;
    }

    static void CreatePointLight(Transform parent, Vector3 position, Color color, float intensity, float range, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
    }

    static void PlaceRock(Transform parent, Terrain terrain, GameObject[] prefabs, Vector3 position, Vector3 baseScale, int seed, bool useTerrainY = true, Material fallbackRockMaterial = null)
    {
        GameObject prefab = PickPrefab(prefabs, seed);
        if (prefab == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;

        if (useTerrainY)
        {
            float y = SampleTerrain(terrain, position.x, position.z);
            position.y = y;
        }

        instance.transform.SetParent(parent);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler((seed % 5) * 6f, (seed * 37f) % 360f, ((seed / 3) % 5) * 4f);

        float scaleJitter = Mathf.Lerp(0.8f, 1.25f, (seed % 11) / 10f);
        instance.transform.localScale = Vector3.Scale(baseScale, Vector3.one * scaleJitter);

        if (fallbackRockMaterial != null)
            ReplaceMaterials(instance, fallbackRockMaterial);
    }

    static GameObject PickPrefab(GameObject[] prefabs, int seed)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        int index = Mathf.Abs(seed) % prefabs.Length;
        return prefabs[index];
    }

    static GameObject LoadPrefab(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static float SampleTerrain(Terrain terrain, float x, float z)
    {
        if (terrain == null)
            return 0f;

        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    static float GetPathMask(float x, float z)
    {
        float mainPath = GetRectMask(x, z, new Rect(84f, 20f, 12f, 122f), 6f);
        float westBranch = GetRectMask(x, z, new Rect(58f, 80f, 32f, 12f), 5f);
        float eastBranch = GetRectMask(x, z, new Rect(90f, 88f, 34f, 12f), 5f);
        float innerRoom = GetRectMask(x, z, InnerRoom, 6f);
        return Mathf.Clamp01(Mathf.Max(Mathf.Max(mainPath, westBranch), Mathf.Max(eastBranch, innerRoom)));
    }

    static float GetRectMask(float x, float z, Rect rect, float falloff)
    {
        float dx = 0f;
        if (x < rect.xMin) dx = rect.xMin - x;
        else if (x > rect.xMax) dx = x - rect.xMax;

        float dz = 0f;
        if (z < rect.yMin) dz = rect.yMin - z;
        else if (z > rect.yMax) dz = z - rect.yMax;

        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        if (distance <= 0.001f)
            return 1f;

        return Mathf.Clamp01(1f - distance / Mathf.Max(0.001f, falloff));
    }

    static Material GetOrCreateEmissiveMaterial(string fileName, Color color, float emission)
    {
        string path = AssetFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(ResolveCompatibleLitShader());
            AssetDatabase.CreateAsset(material, path);
        }

        Shader compatibleShader = ResolveCompatibleLitShader();
        if (compatibleShader != null && material.shader != compatibleShader)
            material.shader = compatibleShader;

        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * emission);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material GetOrCreateRockMaterial(string fileName, Color color)
    {
        string path = AssetFolder + "/" + fileName;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(ResolveCompatibleLitShader());
            AssetDatabase.CreateAsset(material, path);
        }

        Shader compatibleShader = ResolveCompatibleLitShader();
        if (compatibleShader != null && material.shader != compatibleShader)
            material.shader = compatibleShader;

        material.color = color;
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.18f);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.18f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ReplaceMaterials(GameObject root, Material material)
    {
        if (root == null || material == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] mats = renderer.sharedMaterials;
            for (int j = 0; j < mats.Length; j++)
                mats[j] = material;
            renderer.sharedMaterials = mats;
        }
    }

    static Shader ResolveCompatibleLitShader()
    {
        bool hasRenderPipeline = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = hasRenderPipeline
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");

        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        return shader;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void DestroyImmediateIfFound(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
            Object.DestroyImmediate(existing);
    }
}
