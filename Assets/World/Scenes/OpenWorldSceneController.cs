using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-450)]
public class OpenWorldSceneController : MonoBehaviour
{
    static OpenWorldSceneController _active;

    /// <summary>
    /// When <see cref="EnsureForActiveScene"/> creates a controller at runtime, <see cref="Awake"/>
    /// runs before serialized fields are assigned. Skipping bootstrap in Awake avoids one incorrect init pass.
    /// </summary>
    static bool s_deferSceneBootstrap;

    [Header("Scene Identity")]
    public string SceneId = "";
    public OpenWorldSceneType SceneType = OpenWorldSceneType.Route;

    [Header("Transitions")]
    public string BattleSceneName = "BattleScene";
    public string DefaultSpawnPointId = "default";

    [Header("Session Bootstrap")]
    public PokemonData StarterSpeciesOverride;
    [Range(1, 100)] public int StarterLevelOverride = 5;
    public string TrainerNameOverride = "Player";
    [Min(0)] public int StartingPokeBallsOverride = 12;

    [Header("Runtime")]
    public bool EnsurePlayerExists = true;
    public bool CreateDebugPlayerIfMissing = true;
    public bool EnsurePauseMenuExists = true;
    public Vector3 FallbackSpawnPosition = new Vector3(0f, 2f, 0f);

    [Header("Debug")]
    public bool InferredAtRuntime = false;

    bool _initialized;

    public static OpenWorldSceneController Active => _active;
    public string SceneName => gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
    public string EffectiveSceneId => string.IsNullOrWhiteSpace(SceneId) ? SceneName : SceneId.Trim();
    public Transform PlayerTransform { get; private set; }
    public OpenWorldPlayerController PlayerController { get; private set; }

    void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (gameObject.scene == SceneManager.GetActiveScene())
            _active = this;

        if (s_deferSceneBootstrap)
            return;

        InitializeScene();
    }

    void OnEnable()
    {
        if (Application.isPlaying && gameObject.scene == SceneManager.GetActiveScene())
            _active = this;
    }

    void OnDestroy()
    {
        if (_active == this)
            _active = null;
    }

    public void InitializeScene()
    {
        if (_initialized)
            return;

        _initialized = true;

        OpenWorldEncounterManager session = OpenWorldEncounterManager.EnsureSessionExists();
        if (session == null)
            return;

        session.ApplyBootstrapSettings(StarterSpeciesOverride, StarterLevelOverride, TrainerNameOverride, StartingPokeBallsOverride);
        session.RegisterWorldScene(this);

        if (EnsurePauseMenuExists)
            OpenWorldPauseMenu.EnsureExists();

        if (EnsurePlayerExists)
            EnsurePlayerSetup(session);

        session.NotifyWorldSceneReady(this);
        WorldSpawnArea.TopUpAllAreasAfterWorldReady();
    }

    void EnsurePlayerSetup(OpenWorldEncounterManager session)
    {
        bool foundPlayer = OpenWorldSceneLookup.TryGetPlayerObject(out GameObject playerObj);
        bool createdPlayer = false;

        if (!foundPlayer && CreateDebugPlayerIfMissing)
        {
            playerObj = CreateDebugPlayer();
            foundPlayer = playerObj != null;
            createdPlayer = foundPlayer;
        }

        if (!foundPlayer || playerObj == null)
            return;

        session.EnsurePlayerGameplayComponents(playerObj);
        ResolvePlayerReferences(playerObj);
        ApplySpawnIfNeeded(playerObj, createdPlayer, session);
        session.AttachPlayerAvatarIfNeeded(playerObj);
        session.EnsureCalemAnimatorBootstrap(playerObj, refreshRigHierarchy: true);
        WireCamera(PlayerTransform);
    }

    void ResolvePlayerReferences(GameObject playerObj)
    {
        PlayerTransform = playerObj != null ? playerObj.transform : null;
        PlayerController = playerObj != null ? playerObj.GetComponent<OpenWorldPlayerController>() : null;
    }

    void ApplySpawnIfNeeded(GameObject playerObj, bool createdPlayer, OpenWorldEncounterManager session)
    {
        if (playerObj == null || session == null)
            return;

        if (session.TryConsumePendingSceneTravel(SceneName, out OpenWorldEncounterManager.PendingSceneTravel travel))
        {
            ApplyTravelPose(playerObj.transform, travel);
            return;
        }

        if (createdPlayer)
        {
            ResolveDefaultSpawn(out Vector3 position, out Quaternion rotation);
            ApplyTransformPose(playerObj.transform, position, rotation);
        }
    }

    void ApplyTravelPose(Transform player, OpenWorldEncounterManager.PendingSceneTravel travel)
    {
        if (travel == null)
            return;

        if (travel.UseExactWorldPose)
        {
            ApplyTransformPose(player, travel.WorldPosition, Quaternion.Euler(travel.WorldEuler));
            return;
        }

        OpenWorldPlayerStart start = OpenWorldSceneLookup.FindSpawnPoint(travel.SpawnPointId);
        if (start != null)
        {
            Quaternion spawnRotation = start.AlignPlayerRotation ? start.transform.rotation : Quaternion.identity;
            ApplyTransformPose(player, start.transform.position, spawnRotation);
            return;
        }

        ResolveDefaultSpawn(out Vector3 position, out Quaternion rotation);
        ApplyTransformPose(player, position, rotation);
    }

    void ResolveDefaultSpawn(out Vector3 position, out Quaternion rotation)
    {
        OpenWorldPlayerStart start = OpenWorldSceneLookup.FindSpawnPoint(DefaultSpawnPointId);
        if (start != null)
        {
            position = start.transform.position;
            rotation = start.AlignPlayerRotation ? start.transform.rotation : Quaternion.identity;
            return;
        }

        WorldSpawnArea spawnArea = FindFirstSpawnAreaInScene();
        if (spawnArea != null)
        {
            Vector3 probe = spawnArea.transform.position + Vector3.up * 30f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 100f, spawnArea.GroundLayer))
            {
                position = hit.point + Vector3.up;
                rotation = Quaternion.identity;
                return;
            }

            position = spawnArea.transform.position + Vector3.up;
            rotation = Quaternion.identity;
            return;
        }

        position = FallbackSpawnPosition;
        rotation = Quaternion.identity;
    }

    WorldSpawnArea FindFirstSpawnAreaInScene()
    {
        WorldSpawnArea[] areas = Object.FindObjectsByType<WorldSpawnArea>(FindObjectsSortMode.None);
        Scene active = SceneManager.GetActiveScene();
        for (int i = 0; i < areas.Length; i++)
        {
            WorldSpawnArea area = areas[i];
            if (area != null && area.gameObject.scene == active)
                return area;
        }

        return null;
    }

    GameObject CreateDebugPlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player_Debug";
        TryAssignPlayerTag(player);

        Collider primitiveCollider = player.GetComponent<Collider>();
        if (primitiveCollider != null)
            Destroy(primitiveCollider);

        return player;
    }

    void ApplyTransformPose(Transform playerTransform, Vector3 position, Quaternion rotation)
    {
        if (playerTransform == null)
            return;

        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        playerTransform.SetPositionAndRotation(position, rotation);

        if (controller != null)
            controller.enabled = true;
    }

    void WireCamera(Transform playerTransform)
    {
        Camera cam = Camera.main;
        if (cam == null || playerTransform == null)
            return;

        OpenWorldCameraFollow follow = cam.GetComponent<OpenWorldCameraFollow>();
        if (follow != null)
            follow.Target = playerTransform;

        OpenWorldPlayerController controller = playerTransform.GetComponent<OpenWorldPlayerController>();
        OpenWorldMouseLook look = cam.GetComponent<OpenWorldMouseLook>();
        if (look != null && look.ThirdPerson)
        {
            look.BindPlayerForThirdPerson(playerTransform);
            return;
        }

        if (controller != null && controller.CameraTransform == null)
            controller.CameraTransform = cam.transform;
    }

    void TryAssignPlayerTag(GameObject obj)
    {
        if (obj == null)
            return;

        try
        {
            obj.tag = "Player";
        }
        catch (UnityException)
        {
        }
    }

    public string GetNearestSpawnPointId(Vector3 worldPosition)
    {
        OpenWorldPlayerStart[] starts = OpenWorldSceneLookup.FindSpawnPointsInActiveScene();
        if (starts.Length == 0)
            return OpenWorldSceneLookup.NormalizeSpawnPointId(DefaultSpawnPointId);

        OpenWorldPlayerStart best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < starts.Length; i++)
        {
            OpenWorldPlayerStart candidate = starts[i];
            if (candidate == null)
                continue;

            float distance = (candidate.transform.position - worldPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best != null
            ? best.NormalizedSpawnPointId
            : OpenWorldSceneLookup.NormalizeSpawnPointId(DefaultSpawnPointId);
    }

    public static OpenWorldSceneController EnsureForActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!ShouldBootstrapScene(active))
            return null;

        OpenWorldSceneController[] controllers = Object.FindObjectsByType<OpenWorldSceneController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            OpenWorldSceneController controller = controllers[i];
            if (controller != null && controller.gameObject.scene == active)
            {
                _active = controller;
                if (Application.isPlaying)
                    controller.InitializeScene();
                return controller;
            }
        }

        GameObject go = new GameObject("Open World Scene");
        s_deferSceneBootstrap = true;
        OpenWorldSceneController created = null;
        try
        {
            created = go.AddComponent<OpenWorldSceneController>();
            created.SceneId = active.name;
            created.SceneType = InferSceneType(active.name);
            created.BattleSceneName = "BattleScene";
            created.DefaultSpawnPointId = "default";
            created.InferredAtRuntime = true;
            _active = created;
            if (Application.isPlaying)
                created.InitializeScene();
        }
        finally
        {
            s_deferSceneBootstrap = false;
        }

        return created;
    }

    public static bool ShouldBootstrapScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        if (HasComponentInScene<OpenWorldSceneController>(scene))
            return true;

        if (HasComponentInScene<BattleSceneBootstrap>(scene))
            return HasAnyWorldMarker(scene);

        if (HasAnyWorldMarker(scene))
            return true;

        string sceneName = scene.name.ToLowerInvariant();
        return sceneName.Contains("world") ||
               sceneName.Contains("route") ||
               sceneName.Contains("forest") ||
               sceneName.Contains("forrest") ||
               sceneName.Contains("town") ||
               sceneName.Contains("cave") ||
               sceneName.Contains("gym");
    }

    static bool HasAnyWorldMarker(Scene scene)
    {
        return HasComponentInScene<WorldSpawnArea>(scene) ||
               HasComponentInScene<OpenWorldPlayerStart>(scene) ||
               HasComponentInScene<OpenWorldPlayerController>(scene) ||
               HasComponentInScene<OpenWorldCameraFollow>(scene) ||
               HasComponentInScene<OpenWorldMouseLook>(scene);
    }

    static bool HasComponentInScene<T>(Scene scene) where T : Component
    {
        T[] items = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            T item = items[i];
            if (item != null && item.gameObject.scene == scene)
                return true;
        }

        return false;
    }

    static OpenWorldSceneType InferSceneType(string sceneName)
    {
        string normalized = string.IsNullOrWhiteSpace(sceneName)
            ? string.Empty
            : sceneName.ToLowerInvariant();

        if (normalized.Contains("town"))
            return OpenWorldSceneType.Town;
        if (normalized.Contains("cave"))
            return OpenWorldSceneType.Cave;
        if (normalized.Contains("gym"))
            return OpenWorldSceneType.Gym;
        if (normalized.Contains("house") || normalized.Contains("lab") || normalized.Contains("interior"))
            return OpenWorldSceneType.Interior;
        if (normalized.Contains("hub"))
            return OpenWorldSceneType.Hub;

        return OpenWorldSceneType.Route;
    }
}
