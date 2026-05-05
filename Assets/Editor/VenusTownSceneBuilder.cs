using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class VenusTownSceneBuilder
{
    const string TargetSceneName = "VenusTownScene";
    const string GeneratedRootName = "Generated Venus Town";
    const string SystemsRootName = "Generated Venus Town Systems";
    const string TerrainObjectName = "Venus Town Terrain";
    const string AssetFolder = "Assets/Scenes/VenusTownGenerated";
    const string TerrainDataPath = AssetFolder + "/VenusTownTerrainData.asset";

    static readonly Vector3 TerrainSize = new Vector3(180f, 24f, 180f);
    static readonly Rect TownBounds = new Rect(42f, 46f, 96f, 78f);
    static readonly Rect TownSquare = new Rect(70f, 72f, 40f, 28f);

    [MenuItem("Tools/World/Build Venus Town Starter Scene")]
    static void BuildStarterTown()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog("Venus Town Builder", "Open VenusTownScene first, then run this tool again.", "OK");
            return;
        }

        string currentSceneName = Path.GetFileNameWithoutExtension(activeScene.path);
        if (!string.Equals(currentSceneName, TargetSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            bool continueAnyway = EditorUtility.DisplayDialog(
                "Venus Town Builder",
                $"This tool is meant for {TargetSceneName}. The active scene is {currentSceneName}.\n\nBuild anyway?",
                "Build Anyway",
                "Cancel");
            if (!continueAnyway)
                return;
        }

        EnsureFolder(AssetFolder);
        CleanupGeneratedObjects();

        GameObject generatedRoot = new GameObject(GeneratedRootName);
        Terrain terrain = BuildTerrain(generatedRoot.transform);
        BuildTownLayout(generatedRoot.transform, terrain);
        BuildWorldSystems(terrain);
        ConfigureLighting();
        ConfigureCamera();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(activeScene);
        Selection.activeGameObject = generatedRoot;

        EditorUtility.DisplayDialog(
            "Venus Town Ready",
            "Starter town generated.\n\nOpen the scene, press Play, and tweak the layout if you want. Re-running the builder replaces the generated town content only.",
            "OK");
    }

    static void CleanupGeneratedObjects()
    {
        DestroyImmediateIfFound(GeneratedRootName);
        DestroyImmediateIfFound(SystemsRootName);
        DestroyImmediateIfFound(TerrainObjectName);
    }

    static Terrain BuildTerrain(Transform parent)
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        }

        terrainData.heightmapResolution = 257;
        terrainData.alphamapResolution = 256;
        terrainData.baseMapResolution = 1024;
        terrainData.SetDetailResolution(512, 16);
        terrainData.size = TerrainSize;

        TerrainLayer grassLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Terrain/Grass.terrainlayer");
        TerrainLayer dirtLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/BattleScene/Arena/Dirt_Grassland.terrainlayer");
        TerrainLayer rockLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
            "Assets/BattleScene/Arena/Rock_Grassland.terrainlayer");

        if (grassLayer != null && dirtLayer != null && rockLayer != null)
            terrainData.terrainLayers = new[] { grassLayer, dirtLayer, rockLayer };

        terrainData.SetHeights(0, 0, BuildHeights(terrainData));
        if (terrainData.terrainLayers != null && terrainData.terrainLayers.Length >= 3)
            terrainData.SetAlphamaps(0, 0, BuildAlphamaps(terrainData));

        ConfigureGrassDetails(terrainData);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        terrainObject.name = TerrainObjectName;
        terrainObject.transform.SetParent(parent);
        terrainObject.transform.position = Vector3.zero;

        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.detailObjectDensity = 0.7f;
        terrain.treeDistance = 200f;
        terrain.detailObjectDistance = 80f;
        terrain.basemapDistance = 1000f;

        TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
        if (collider != null)
            collider.terrainData = terrainData;

        return terrain;
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
                float edgeRing = Mathf.Clamp01((28f - edgeDistance) / 28f);
                float townBlend = GetTownFlattenBlend(worldX, worldZ);
                float pathBlend = Mathf.Clamp01(GetPathMask(worldX, worldZ) * 1.2f);

                float noise = Mathf.PerlinNoise(worldX * 0.026f, worldZ * 0.026f) * 0.01f;
                float outerHills = edgeRing * edgeRing * 0.13f;
                float baseHeight = 0.018f + noise + outerHills;

                float flattened = Mathf.Lerp(baseHeight, 0.022f, townBlend);
                heights[z, x] = Mathf.Lerp(flattened, 0.021f, pathBlend);
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

                float grass = 1f;
                float dirt = 0f;
                float rock = 0f;

                float pathMask = GetPathMask(worldX, worldZ);
                float plazaMask = GetRectMask(worldX, worldZ, TownSquare, 4f);
                float edgeDistance = Mathf.Min(Mathf.Min(worldX, TerrainSize.x - worldX), Mathf.Min(worldZ, TerrainSize.z - worldZ));
                float rockMask = Mathf.Clamp01((16f - edgeDistance) / 16f);

                dirt = Mathf.Clamp01(Mathf.Max(pathMask, plazaMask));
                rock = rockMask * 0.75f * (1f - dirt);
                grass = Mathf.Clamp01(1f - dirt - rock);

                float sum = grass + dirt + rock;
                maps[z, x, 0] = grass / sum;
                maps[z, x, 1] = dirt / sum;
                maps[z, x, 2] = rock / sum;
            }
        }

        return maps;
    }

    static void ConfigureGrassDetails(TerrainData terrainData)
    {
        Texture2D detailTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Textures/Terrain Grass.png");
        if (detailTexture == null)
            return;

        DetailPrototype grass = new DetailPrototype
        {
            prototypeTexture = detailTexture,
            renderMode = DetailRenderMode.GrassBillboard,
            minWidth = 0.8f,
            maxWidth = 1.25f,
            minHeight = 0.7f,
            maxHeight = 1.45f,
            noiseSpread = 0.12f,
            healthyColor = new Color(0.44f, 0.72f, 0.32f, 1f),
            dryColor = new Color(0.72f, 0.84f, 0.36f, 1f)
        };

        terrainData.detailPrototypes = new[] { grass };

        int resolution = terrainData.detailWidth;
        int[,] density = new int[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX = x / (float)(resolution - 1) * TerrainSize.x;
                float worldZ = z / (float)(resolution - 1) * TerrainSize.z;

                if (GetTownFlattenBlend(worldX, worldZ) > 0.55f || GetPathMask(worldX, worldZ) > 0.15f)
                    continue;

                float edgeDistance = Mathf.Min(Mathf.Min(worldX, TerrainSize.x - worldX), Mathf.Min(worldZ, TerrainSize.z - worldZ));
                if (edgeDistance < 7f)
                    continue;

                float noise = Mathf.PerlinNoise(worldX * 0.12f, worldZ * 0.12f);
                density[z, x] = noise > 0.48f ? Mathf.RoundToInt(Mathf.Lerp(1f, 6f, noise)) : 0;
            }
        }

        terrainData.SetDetailLayer(0, 0, 0, density);
    }

    static void BuildTownLayout(Transform root, Terrain terrain)
    {
        Transform townRoot = new GameObject("Town Layout").transform;
        townRoot.SetParent(root);

        Material wallCream = GetOrCreateMaterial("Town_WallCream.mat", new Color(0.95f, 0.9f, 0.78f));
        Material wallBlue = GetOrCreateMaterial("Town_WallBlue.mat", new Color(0.73f, 0.85f, 0.97f));
        Material roofRed = GetOrCreateMaterial("Town_RoofRed.mat", new Color(0.73f, 0.27f, 0.26f));
        Material roofBlue = GetOrCreateMaterial("Town_RoofBlue.mat", new Color(0.29f, 0.4f, 0.67f));
        Material wood = GetOrCreateMaterial("Town_Wood.mat", new Color(0.48f, 0.31f, 0.18f));
        Material accent = GetOrCreateMaterial("Town_Accent.mat", new Color(0.95f, 0.95f, 0.98f));
        Material stone = GetOrCreateMaterial("Town_Stone.mat", new Color(0.52f, 0.56f, 0.58f));

        CreateHouse(townRoot, terrain, "Player House", new Vector3(90f, 0f, 60f), new Vector3(12f, 5.5f, 10f), wallBlue, roofRed, wood);
        CreateHouse(townRoot, terrain, "West House", new Vector3(60f, 0f, 86f), new Vector3(10f, 4.8f, 9f), wallCream, roofBlue, wood);
        CreateHouse(townRoot, terrain, "East House", new Vector3(120f, 0f, 86f), new Vector3(10f, 4.8f, 9f), wallCream, roofBlue, wood);
        CreateHouse(townRoot, terrain, "Professor Lab", new Vector3(90f, 0f, 116f), new Vector3(18f, 6.2f, 12f), accent, roofBlue, wood);

        CreateFountain(townRoot, terrain, new Vector3(90f, 0f, 86f), stone, accent);
        CreateSign(townRoot, terrain, "Town Sign", new Vector3(90f, 0f, 72f), wood, accent, new Vector3(2.6f, 1.1f, 0.22f));
        CreateSign(townRoot, terrain, "Route Sign", new Vector3(90f, 0f, 132f), wood, accent, new Vector3(2.3f, 1.0f, 0.22f));

        CreateFenceRing(townRoot, terrain, new Rect(46f, 52f, 88f, 74f), wood, 90f);
        CreateLampPair(townRoot, terrain, 77f, 77f, wood, stone);
        CreateLampPair(townRoot, terrain, 103f, 77f, wood, stone);
        CreateLampPair(townRoot, terrain, 77f, 97f, wood, stone);
        CreateLampPair(townRoot, terrain, 103f, 97f, wood, stone);

        PopulateTownTrees(root, terrain);
        CreateBoundaryWalls(root, terrain);
    }

    static void BuildWorldSystems(Terrain terrain)
    {
        GameObject systemsRoot = new GameObject(SystemsRootName);

        GameObject start = new GameObject("Player Start");
        start.transform.SetParent(systemsRoot.transform);
        float startY = SampleTerrain(terrain, 90f, 68f);
        start.transform.position = new Vector3(90f, startY + 1f, 68f);
        start.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        OpenWorldPlayerStart startMarker = start.AddComponent<OpenWorldPlayerStart>();
        startMarker.SpawnPointId = "town_start";
        startMarker.UseAsDefaultSpawn = true;

        GameObject sceneControllerObject = new GameObject("Open World Scene");
        sceneControllerObject.transform.SetParent(systemsRoot.transform);
        OpenWorldSceneController sceneController = sceneControllerObject.AddComponent<OpenWorldSceneController>();
        sceneController.SceneId = TargetSceneName;
        sceneController.SceneType = OpenWorldSceneType.Town;
        sceneController.BattleSceneName = "BattleScene";
        sceneController.DefaultSpawnPointId = startMarker.SpawnPointId;
        sceneController.FallbackSpawnPosition = start.transform.position;
        sceneController.StarterSpeciesOverride = AssetDatabase.LoadAssetAtPath<PokemonData>("Assets/BattleScene/Pikachu.asset");
        sceneController.StarterLevelOverride = 5;
        sceneController.TrainerNameOverride = "Player";
        sceneController.StartingPokeBallsOverride = 12;

        GameObject spawnZone = new GameObject("North Route Spawn");
        spawnZone.transform.SetParent(systemsRoot.transform);
        spawnZone.transform.position = new Vector3(90f, 0f, 148f);
        WorldSpawnArea area = spawnZone.AddComponent<WorldSpawnArea>();
        area.RefillMode = WorldSpawnRefillMode.Continuous;
        area.SingleWildPokemonMode = false;
        area.ParticipateInGlobalWildCap = false;
        area.Radius = 18f;
        area.MaxAlivePokemon = 5;
        area.SpawnInterval = 2.25f;
        area.HeightOffset = 1.2f;
        area.DespawnDistanceFromPlayer = 70f;
        area.SpawnNearPlayerInSingleMode = false;
        area.FollowPlayerMovementForSpawns = false;
        area.MinDistanceFromPlayer = 14f;
        area.MinDistanceBetweenWilds = 14f;
        area.SpawnCandidateSamples = 16;
        area.AvoidSpawningInPlayerSight = true;
        area.VisibleSpawnPenalty = 300f;
        area.FrontConeSpawnPenalty = 120f;
        area.SpawnProfile = AssetDatabase.LoadAssetAtPath<WorldSpawnProfile>(GameContentAssetPaths.ForrestSpawnProfileAssetPath);
    }

    static void ConfigureLighting()
    {
        Light light = Object.FindObjectOfType<Light>();
        if (light == null || light.type != LightType.Directional)
        {
            GameObject go = light == null ? new GameObject("Directional Light") : light.gameObject;
            light = go.GetComponent<Light>();
            if (light == null)
                light = go.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.color = new Color(1f, 0.96f, 0.86f, 1f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
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
        look.OrbitDistance = 8.2f;
        look.OrbitVerticalLift = 2.9f;
        look.LookAtHeight = 1.4f;
        look.MouseSensitivity = 1.8f;
        look.MinPitch = 16f;
        look.MaxPitch = 50f;
        cam.transform.position = new Vector3(90f, 12f, 58f);
        cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
    }

    static void CreateHouse(Transform parent, Terrain terrain, string name, Vector3 center, Vector3 size, Material wallMaterial, Material roofMaterial, Material trimMaterial)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent);

        float groundY = SampleTerrain(terrain, center.x, center.z);
        float bodyHeight = size.y;
        float roofHeight = 1.4f;

        CreatePrimitive(root, PrimitiveType.Cube, "Body", new Vector3(center.x, groundY + bodyHeight * 0.5f, center.z), new Vector3(size.x, bodyHeight, size.z), wallMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Roof", new Vector3(center.x, groundY + bodyHeight + roofHeight * 0.5f, center.z), new Vector3(size.x + 1.2f, roofHeight, size.z + 1.2f), roofMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Door", new Vector3(center.x, groundY + 1.2f, center.z - size.z * 0.5f + 0.2f), new Vector3(1.5f, 2.4f, 0.3f), trimMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Window Left", new Vector3(center.x - size.x * 0.22f, groundY + 2.6f, center.z - size.z * 0.5f + 0.15f), new Vector3(1.8f, 1.2f, 0.2f), trimMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Window Right", new Vector3(center.x + size.x * 0.22f, groundY + 2.6f, center.z - size.z * 0.5f + 0.15f), new Vector3(1.8f, 1.2f, 0.2f), trimMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Porch", new Vector3(center.x, groundY + 0.18f, center.z - size.z * 0.5f - 0.9f), new Vector3(3.4f, 0.36f, 2.4f), trimMaterial);
    }

    static void CreateFountain(Transform parent, Terrain terrain, Vector3 center, Material baseMaterial, Material waterMaterial)
    {
        float y = SampleTerrain(terrain, center.x, center.z);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Fountain Base", new Vector3(center.x, y + 0.4f, center.z), new Vector3(4f, 0.35f, 4f), baseMaterial);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Fountain Water", new Vector3(center.x, y + 0.6f, center.z), new Vector3(3.2f, 0.08f, 3.2f), waterMaterial);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Fountain Column", new Vector3(center.x, y + 1.1f, center.z), new Vector3(0.7f, 0.55f, 0.7f), baseMaterial);
        CreatePrimitive(parent, PrimitiveType.Sphere, "Fountain Top", new Vector3(center.x, y + 1.95f, center.z), new Vector3(0.8f, 0.8f, 0.8f), waterMaterial);
    }

    static void CreateSign(Transform parent, Terrain terrain, string name, Vector3 center, Material postMaterial, Material boardMaterial, Vector3 boardScale)
    {
        Transform root = new GameObject(name).transform;
        root.SetParent(parent);
        float y = SampleTerrain(terrain, center.x, center.z);
        CreatePrimitive(root, PrimitiveType.Cube, "Post", new Vector3(center.x, y + 1.2f, center.z), new Vector3(0.25f, 2.4f, 0.25f), postMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Board", new Vector3(center.x, y + 2.2f, center.z), boardScale, boardMaterial);
    }

    static void CreateFenceRing(Transform parent, Terrain terrain, Rect rect, Material material, float openingCenterX)
    {
        for (float x = rect.xMin; x <= rect.xMax; x += 2.2f)
        {
            if (Mathf.Abs(x - openingCenterX) < 8f)
                continue;

            CreateFencePost(parent, terrain, new Vector3(x, 0f, rect.yMax), material);
        }

        for (float z = rect.yMin; z <= rect.yMax; z += 2.2f)
        {
            CreateFencePost(parent, terrain, new Vector3(rect.xMin, 0f, z), material);
            CreateFencePost(parent, terrain, new Vector3(rect.xMax, 0f, z), material);
        }

        for (float x = rect.xMin; x <= rect.xMax; x += 2.2f)
            CreateFencePost(parent, terrain, new Vector3(x, 0f, rect.yMin), material);
    }

    static void CreateFencePost(Transform parent, Terrain terrain, Vector3 position, Material material)
    {
        float y = SampleTerrain(terrain, position.x, position.z);
        CreatePrimitive(parent, PrimitiveType.Cube, "Fence", new Vector3(position.x, y + 0.7f, position.z), new Vector3(0.22f, 1.4f, 0.22f), material);
    }

    static void CreateLampPair(Transform parent, Terrain terrain, float x, float z, Material postMaterial, Material lightMaterial)
    {
        float y = SampleTerrain(terrain, x, z);
        CreatePrimitive(parent, PrimitiveType.Cylinder, "Lamp Post", new Vector3(x, y + 1.5f, z), new Vector3(0.16f, 1.5f, 0.16f), postMaterial);
        CreatePrimitive(parent, PrimitiveType.Sphere, "Lamp Light", new Vector3(x, y + 3.15f, z), new Vector3(0.45f, 0.45f, 0.45f), lightMaterial);
    }

    static void PopulateTownTrees(Transform root, Terrain terrain)
    {
        GameObject sprucePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Trees/Spruce 1.prefab");
        GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/3rd Party/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Bush/Bush.prefab");

        if (sprucePrefab == null && bushPrefab == null)
            return;

        Transform foliageRoot = new GameObject("Boundary Foliage").transform;
        foliageRoot.SetParent(root);

        for (int i = 0; i < 30; i++)
        {
            float t = i / 30f;
            Vector3 posA = new Vector3(Mathf.Lerp(12f, 168f, t), 0f, 18f + Mathf.Sin(t * Mathf.PI * 5f) * 2f);
            Vector3 posB = new Vector3(Mathf.Lerp(12f, 168f, t), 0f, 162f + Mathf.Cos(t * Mathf.PI * 5f) * 2f);
            TryPlaceFoliage(foliageRoot, terrain, sprucePrefab, bushPrefab, posA, i);
            TryPlaceFoliage(foliageRoot, terrain, sprucePrefab, bushPrefab, posB, i + 30);
        }

        for (int i = 0; i < 18; i++)
        {
            float t = i / 18f;
            Vector3 posLeft = new Vector3(14f + Mathf.Sin(t * Mathf.PI * 5f) * 2f, 0f, Mathf.Lerp(24f, 156f, t));
            Vector3 posRight = new Vector3(166f + Mathf.Cos(t * Mathf.PI * 5f) * 2f, 0f, Mathf.Lerp(24f, 156f, t));
            TryPlaceFoliage(foliageRoot, terrain, sprucePrefab, bushPrefab, posLeft, i + 60);
            TryPlaceFoliage(foliageRoot, terrain, sprucePrefab, bushPrefab, posRight, i + 90);
        }
    }

    static void TryPlaceFoliage(Transform parent, Terrain terrain, GameObject treePrefab, GameObject bushPrefab, Vector3 position, int seed)
    {
        GameObject prefab = seed % 4 == 0 && bushPrefab != null ? bushPrefab : treePrefab;
        if (prefab == null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;

        instance.name = prefab.name;
        instance.transform.SetParent(parent);
        float y = SampleTerrain(terrain, position.x, position.z);
        instance.transform.position = new Vector3(position.x, y, position.z);
        instance.transform.rotation = Quaternion.Euler(0f, (seed * 37f) % 360f, 0f);
        float scale = prefab == bushPrefab ? Mathf.Lerp(0.9f, 1.25f, (seed % 7) / 6f) : Mathf.Lerp(1.1f, 1.5f, (seed % 9) / 8f);
        instance.transform.localScale = Vector3.one * scale;
    }

    static void CreateBoundaryWalls(Transform root, Terrain terrain)
    {
        Transform boundsRoot = new GameObject("Scene Bounds").transform;
        boundsRoot.SetParent(root);

        CreateBoundaryWall(boundsRoot, new Vector3(90f, 3f, -1f), new Vector3(182f, 6f, 2f));
        CreateBoundaryWall(boundsRoot, new Vector3(90f, 3f, 181f), new Vector3(182f, 6f, 2f));
        CreateBoundaryWall(boundsRoot, new Vector3(-1f, 3f, 90f), new Vector3(2f, 6f, 182f));
        CreateBoundaryWall(boundsRoot, new Vector3(181f, 3f, 90f), new Vector3(2f, 6f, 182f));
    }

    static void CreateBoundaryWall(Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject wall = new GameObject("Invisible Boundary");
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = scale;
    }

    static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return go;
    }

    static Material GetOrCreateMaterial(string fileName, Color color)
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
        EditorUtility.SetDirty(material);
        return material;
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

    static float SampleTerrain(Terrain terrain, float x, float z)
    {
        if (terrain == null)
            return 0f;

        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    static float GetTownFlattenBlend(float x, float z)
    {
        float townMask = GetRectMask(x, z, TownBounds, 18f);
        float plazaMask = GetRectMask(x, z, TownSquare, 8f);
        return Mathf.Clamp01(Mathf.Max(townMask, plazaMask));
    }

    static float GetPathMask(float x, float z)
    {
        float vertical = GetRectMask(x, z, new Rect(84f, 42f, 12f, 96f), 5f);
        float horizontal = GetRectMask(x, z, new Rect(54f, 80f, 72f, 12f), 5f);
        float playerYard = GetRectMask(x, z, new Rect(84f, 54f, 12f, 16f), 4f);
        float labApron = GetRectMask(x, z, new Rect(80f, 108f, 20f, 16f), 4f);
        return Mathf.Clamp01(Mathf.Max(Mathf.Max(vertical, horizontal), Mathf.Max(playerYard, labApron)));
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
