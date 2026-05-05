using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Persistent world session: party, bag, dex, encounter transfer, and scene travel state.
/// Scene-local setup is handled by <see cref="OpenWorldSceneController"/>.
/// </summary>
[DefaultExecutionOrder(-500)]
public class OpenWorldEncounterManager : MonoBehaviour
{
    [System.Serializable]
    public class BagItemStack
    {
        public string ItemId;
        public string DisplayName;
        public int Count;
    }

    public enum PokemonCollectionDestination
    {
        Party,
        Storage
    }

    [System.Serializable]
    public class EncounterPayload
    {
        public PokemonInstance WildPokemon;
        public Vector3 WorldPosition;
        public GameObject EncounterObject;
        public bool DestroyEncounterObject;
        public string ReturnSceneName;
        public string ReturnSpawnPointId;
        public Vector3 ReturnPlayerPosition;
        public Vector3 ReturnPlayerEuler;
        public bool UseReturnPlayerPose;
    }

    [System.Serializable]
    public class PendingSceneTravel
    {
        public string SceneName;
        public string SpawnPointId;
        public bool UseExactWorldPose;
        public Vector3 WorldPosition;
        public Vector3 WorldEuler;
    }

    public const int MaxPartySize = 6;
    public const string PokeBallItemId = "pokeball";
    public const string PokeBallDisplayName = "Poke Ball";

    public static OpenWorldEncounterManager Instance { get; private set; }

    [Header("Scene Defaults")]
    [FormerlySerializedAs("WorldSceneName")]
    public string LegacyWorldSceneName = "";
    public string BattleSceneName = "BattleScene";

    [Header("Player Battle Setup")]
    public PokemonData StarterSpecies;
    [Range(1, 100)] public int StarterLevel = 5;

    [Header("Player Avatar")]
    [Tooltip("Resources path to the visual prefab (no extension), e.g. Character/Calem/CalemVisual. Parent folder is used when scanning for Avatar sub-assets.")]
    public string PlayerAvatarResourcePath = "Character/Calem/CalemVisual";
    [Tooltip("Optional. If set on the session, used when the player has no CalemController assigned on CalemOpenWorldAnimatorBootstrap.")]
    public RuntimeAnimatorController OpenWorldPlayerAnimatorOverride;
    /// <summary>Runtime load path (no .controller) for <see cref="CalemOpenWorldAnimatorBootstrap"/> when override is null.</summary>
    public const string DefaultOpenWorldAnimatorResourcesPath = "OpenWorld/CalemOpenWorld";
    public Vector3 PlayerAvatarLocalPosition = Vector3.zero;
    public Vector3 PlayerAvatarLocalEuler = Vector3.zero;
    public Vector3 PlayerAvatarLocalScale = Vector3.one;

    [Header("Trainer")]
    public string PlayerTrainerName = "Player";

    [Header("Inventory")]
    [Min(0)] public int StartingPokeBalls = 12;

    [SerializeField] List<PokemonInstance> _party = new List<PokemonInstance>(MaxPartySize);
    [SerializeField] List<PokemonInstance> _storage = new List<PokemonInstance>();
    [SerializeField] List<int> _seenDexNumbers = new List<int>();
    [SerializeField] List<int> _caughtDexNumbers = new List<int>();
    [SerializeField] List<BagItemStack> _bagItems = new List<BagItemStack>();
    [SerializeField] PendingSceneTravel _pendingSceneTravel;
    [SerializeField] bool _bootstrapSettingsApplied;

    string _currentWorldSceneName = "";
    bool _pendingWorldWildRespawn;

    static bool s_loggedMissingOpenWorldAnimatorController;

    public int PlayerTrainerID { get; private set; }
    public PokemonInstance PlayerLeadPokemon => _party.Count > 0 ? _party[0] : null;
    public IReadOnlyList<PokemonInstance> Party => _party;
    public IReadOnlyList<PokemonInstance> Storage => _storage;
    public IReadOnlyList<BagItemStack> BagItems => _bagItems;
    public EncounterPayload CurrentEncounter { get; private set; }
    public bool HasPendingEncounter => CurrentEncounter != null;
    public int PartyCount => _party.Count;
    public int StorageCount => _storage.Count;
    public int PokeBallCount => GetItemCount(PokeBallItemId);
    public string CurrentWorldSceneName => _currentWorldSceneName;

    public static OpenWorldEncounterManager EnsureSessionExists()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("Open World Session");
        return go.AddComponent<OpenWorldEncounterManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (PlayerTrainerID == 0)
            PlayerTrainerID = Random.Range(100000, 999999);

        EnsureStarterParty();
        EnsureStarterInventory();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!IsBattleScene(activeScene.name))
            _currentWorldSceneName = activeScene.name;
    }

    void Start()
    {
        if (Application.isPlaying)
            OpenWorldSceneController.EnsureForActiveScene();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying)
            return;

        OpenWorldSceneController controller = OpenWorldSceneController.EnsureForActiveScene();
        if (controller == null && !IsBattleScene(scene.name))
            _currentWorldSceneName = scene.name;
    }

    public void RegisterWorldScene(OpenWorldSceneController controller)
    {
        if (controller == null)
            return;

        _currentWorldSceneName = controller.SceneName;
        if (!string.IsNullOrWhiteSpace(controller.BattleSceneName))
            BattleSceneName = controller.BattleSceneName;
    }

    public void NotifyWorldSceneReady(OpenWorldSceneController controller)
    {
        if (controller == null)
            return;

        _currentWorldSceneName = controller.SceneName;
        if (_pendingWorldWildRespawn)
            _pendingWorldWildRespawn = false;
    }

    public void ApplyBootstrapSettings(PokemonData starterSpecies, int starterLevel, string trainerName, int startingPokeBalls)
    {
        if (_bootstrapSettingsApplied)
            return;

        if (starterSpecies != null)
            StarterSpecies = starterSpecies;

        if (starterLevel > 0)
            StarterLevel = starterLevel;

        if (!string.IsNullOrWhiteSpace(trainerName))
            PlayerTrainerName = trainerName;

        if (startingPokeBalls >= 0)
            StartingPokeBalls = startingPokeBalls;

        EnsureStarterParty();
        EnsureStarterInventory();
        _bootstrapSettingsApplied = true;
    }

    public bool TravelToScene(string sceneName, string spawnPointId = "")
    {
        if (!CanLoadScene(sceneName))
        {
            Debug.LogError($"[OpenWorldEncounterManager] Scene '{sceneName}' is not loadable.");
            return false;
        }

        _pendingSceneTravel = new PendingSceneTravel
        {
            SceneName = sceneName,
            SpawnPointId = OpenWorldSceneLookup.NormalizeSpawnPointId(spawnPointId),
            UseExactWorldPose = false,
            WorldPosition = Vector3.zero,
            WorldEuler = Vector3.zero
        };
        _pendingWorldWildRespawn = false;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public bool TryConsumePendingSceneTravel(string sceneName, out PendingSceneTravel travel)
    {
        if (_pendingSceneTravel != null &&
            string.Equals(_pendingSceneTravel.SceneName, sceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            travel = _pendingSceneTravel;
            _pendingSceneTravel = null;
            return true;
        }

        travel = null;
        return false;
    }

    public void StartWildEncounter(PokemonInstance wildPokemon, Vector3 worldPos, GameObject encounterObj, bool destroyEncounterObject)
    {
        if (wildPokemon == null || HasPendingEncounter)
            return;

        MarkSpeciesSeen(wildPokemon.SpeciesData);

        string battleSceneName = ResolveBattleSceneName();
        if (!CanLoadScene(battleSceneName))
        {
            Debug.LogError($"[OpenWorldEncounterManager] Battle scene '{battleSceneName}' is not loadable.");
            return;
        }

        if (PlayerLeadPokemon == null)
        {
            if (wildPokemon.SpeciesData != null)
                AddPokemonToCollection(PokemonFactory.CreatePlayerPokemon(wildPokemon.SpeciesData, Mathf.Max(5, wildPokemon.Level)));
            else
            {
                Debug.LogWarning("[OpenWorldEncounterManager] Player lead Pokemon is not configured.");
                return;
            }
        }

        string returnSceneName = !string.IsNullOrWhiteSpace(_currentWorldSceneName)
            ? _currentWorldSceneName
            : SceneManager.GetActiveScene().name;
        string returnSpawnPointId = OpenWorldSceneLookup.NormalizeSpawnPointId(
            OpenWorldSceneController.Active != null
                ? OpenWorldSceneController.Active.GetNearestSpawnPointId(worldPos)
                : string.Empty);

        Vector3 returnPlayerPosition = worldPos;
        Vector3 returnPlayerEuler = Vector3.zero;
        bool useReturnPlayerPose = false;

        if (OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
        {
            returnPlayerPosition = playerTransform.position;
            returnPlayerEuler = playerTransform.eulerAngles;
            useReturnPlayerPose = true;
        }

        CurrentEncounter = new EncounterPayload
        {
            WildPokemon = wildPokemon,
            WorldPosition = worldPos,
            EncounterObject = encounterObj,
            DestroyEncounterObject = destroyEncounterObject,
            ReturnSceneName = returnSceneName,
            ReturnSpawnPointId = returnSpawnPointId,
            ReturnPlayerPosition = returnPlayerPosition,
            ReturnPlayerEuler = returnPlayerEuler,
            UseReturnPlayerPose = useReturnPlayerPose
        };

        SceneManager.LoadScene(battleSceneName);
    }

    public void CompleteEncounter(BattleResolution resolution)
    {
        if (CurrentEncounter != null && CurrentEncounter.DestroyEncounterObject && CurrentEncounter.EncounterObject != null)
            Destroy(CurrentEncounter.EncounterObject);

        string destinationScene = ResolveReturnSceneName();
        if (!CanLoadScene(destinationScene))
        {
            Debug.LogError($"[OpenWorldEncounterManager] World scene '{destinationScene}' is not loadable.");
            CurrentEncounter = null;
            _pendingSceneTravel = null;
            _pendingWorldWildRespawn = false;
            return;
        }

        EncounterPayload resolvedEncounter = CurrentEncounter;
        CurrentEncounter = null;
        _pendingWorldWildRespawn = true;
        _pendingSceneTravel = new PendingSceneTravel
        {
            SceneName = destinationScene,
            SpawnPointId = resolvedEncounter != null
                ? OpenWorldSceneLookup.NormalizeSpawnPointId(resolvedEncounter.ReturnSpawnPointId)
                : string.Empty,
            UseExactWorldPose = resolvedEncounter != null && resolvedEncounter.UseReturnPlayerPose,
            WorldPosition = resolvedEncounter != null ? resolvedEncounter.ReturnPlayerPosition : Vector3.zero,
            WorldEuler = resolvedEncounter != null ? resolvedEncounter.ReturnPlayerEuler : Vector3.zero
        };
        SceneManager.LoadScene(destinationScene);
    }

    string ResolveReturnSceneName()
    {
        if (CurrentEncounter != null && !string.IsNullOrWhiteSpace(CurrentEncounter.ReturnSceneName))
            return CurrentEncounter.ReturnSceneName;

        if (!string.IsNullOrWhiteSpace(_currentWorldSceneName))
            return _currentWorldSceneName;

        return LegacyWorldSceneName;
    }

    string ResolveBattleSceneName()
    {
        if (OpenWorldSceneController.Active != null && !string.IsNullOrWhiteSpace(OpenWorldSceneController.Active.BattleSceneName))
            return OpenWorldSceneController.Active.BattleSceneName;

        return BattleSceneName;
    }

    bool IsBattleScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        string battleSceneName = ResolveBattleSceneName();
        if (!string.IsNullOrWhiteSpace(battleSceneName) &&
            string.Equals(sceneName, battleSceneName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        return sceneName.IndexOf("battle", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void EnsureStarterParty()
    {
        if (_party.Count > 0 || StarterSpecies == null)
            return;

        PokemonInstance starter = PokemonFactory.CreatePlayerPokemon(StarterSpecies, StarterLevel);
        AddPokemonToCollection(starter);
    }

    void EnsureStarterInventory()
    {
        if (GetItemCount(PokeBallItemId) > 0 || StartingPokeBalls <= 0)
            return;

        AddItem(PokeBallItemId, PokeBallDisplayName, StartingPokeBalls);
    }

    public PokemonCollectionDestination AddPokemonToCollection(PokemonInstance pokemon)
    {
        if (pokemon == null)
            return PokemonCollectionDestination.Storage;

        ClaimOwnership(pokemon);
        MarkSpeciesCaught(pokemon.SpeciesData);
        if (string.IsNullOrWhiteSpace(pokemon.Nickname) && pokemon.SpeciesData != null)
            pokemon.Nickname = pokemon.SpeciesData.PokemonName;

        if (_party.Count < MaxPartySize)
        {
            _party.Add(pokemon);
            return PokemonCollectionDestination.Party;
        }

        _storage.Add(pokemon);
        return PokemonCollectionDestination.Storage;
    }

    public PokemonCollectionDestination CaptureWildPokemon(PokemonInstance pokemon)
    {
        return AddPokemonToCollection(pokemon);
    }

    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _bagItems == null)
            return 0;

        string normalizedId = NormalizeItemId(itemId);
        for (int i = 0; i < _bagItems.Count; i++)
        {
            BagItemStack stack = _bagItems[i];
            if (stack == null || NormalizeItemId(stack.ItemId) != normalizedId)
                continue;

            return Mathf.Max(0, stack.Count);
        }

        return 0;
    }

    public void AddItem(string itemId, string displayName, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return;

        string normalizedId = NormalizeItemId(itemId);
        BagItemStack stack = FindBagStack(normalizedId);
        if (stack == null)
        {
            stack = new BagItemStack
            {
                ItemId = normalizedId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? itemId : displayName,
                Count = amount
            };
            _bagItems.Add(stack);
            return;
        }

        if (string.IsNullOrWhiteSpace(stack.DisplayName) && !string.IsNullOrWhiteSpace(displayName))
            stack.DisplayName = displayName;

        stack.Count = Mathf.Max(0, stack.Count + amount);
    }

    public bool TryConsumeItem(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return false;

        BagItemStack stack = FindBagStack(NormalizeItemId(itemId));
        if (stack == null || stack.Count < amount)
            return false;

        stack.Count -= amount;
        return true;
    }

    public bool TryConsumePokeBall()
    {
        return TryConsumeItem(PokeBallItemId, 1);
    }

    public string GetDisplayNameForItem(string itemId)
    {
        BagItemStack stack = FindBagStack(NormalizeItemId(itemId));
        if (stack != null && !string.IsNullOrWhiteSpace(stack.DisplayName))
            return stack.DisplayName;

        if (NormalizeItemId(itemId) == PokeBallItemId)
            return PokeBallDisplayName;

        return itemId;
    }

    public bool SetLeadPokemon(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= _party.Count)
            return false;

        if (partyIndex == 0)
            return true;

        PokemonInstance selected = _party[partyIndex];
        _party[partyIndex] = _party[0];
        _party[0] = selected;
        return true;
    }

    public int IndexOfPartyMember(PokemonInstance pokemon)
    {
        return pokemon == null ? -1 : _party.IndexOf(pokemon);
    }

    public bool HasOtherUsablePartyMember(PokemonInstance current)
    {
        for (int i = 0; i < _party.Count; i++)
        {
            PokemonInstance candidate = _party[i];
            if (candidate != null && candidate != current && !candidate.IsFainted)
                return true;
        }

        return false;
    }

    public bool TryGetNextUsablePartyMember(PokemonInstance current, out PokemonInstance next, out int index)
    {
        for (int i = 0; i < _party.Count; i++)
        {
            PokemonInstance candidate = _party[i];
            if (candidate == null || candidate == current || candidate.IsFainted)
                continue;

            next = candidate;
            index = i;
            return true;
        }

        next = null;
        index = -1;
        return false;
    }

    public bool HasSeenSpecies(PokemonData species)
    {
        return species != null && species.DexNumber > 0 && _seenDexNumbers.Contains(species.DexNumber);
    }

    public bool HasCaughtSpecies(PokemonData species)
    {
        return species != null && species.DexNumber > 0 && _caughtDexNumbers.Contains(species.DexNumber);
    }

    public void MarkSpeciesSeen(PokemonData species)
    {
        RegisterDexNumber(_seenDexNumbers, species);
    }

    public void MarkSpeciesCaught(PokemonData species)
    {
        RegisterDexNumber(_seenDexNumbers, species);
        RegisterDexNumber(_caughtDexNumbers, species);
    }

    public void EnsurePlayerGameplayComponents(GameObject playerObj)
    {
        if (playerObj == null)
            return;

        TryAssignPlayerTag(playerObj);

        CharacterController controller = playerObj.GetComponent<CharacterController>();
        if (controller == null)
            controller = playerObj.AddComponent<CharacterController>();

        if (controller.height < 0.1f) controller.height = 2f;
        if (controller.radius < 0.1f) controller.radius = 0.4f;
        if (controller.center == Vector3.zero) controller.center = new Vector3(0f, 1f, 0f);
        if (controller.stepOffset <= 0f) controller.stepOffset = 0.3f;
        controller.enabled = true;

        if (playerObj.GetComponent<OpenWorldPlayerController>() == null)
            playerObj.AddComponent<OpenWorldPlayerController>();

        // Do not call EnsureCalemAnimatorBootstrap here — PlayerAvatarVisual is usually created afterward in
        // AttachPlayerAvatarIfNeeded; binding early leaves a stale TargetAnimator or binds before the rig exists.
    }

    /// <summary>
    /// Ensures <see cref="CalemOpenWorldAnimatorBootstrap"/> exists, assigns a locomotion controller, and applies it to the rig.
    /// </summary>
    /// <param name="refreshRigHierarchy">True after swapping the visual (e.g. new PlayerAvatarVisual); clears cached Animator and rebinds.</param>
    public void EnsureCalemAnimatorBootstrap(GameObject playerObj, bool refreshRigHierarchy = false)
    {
        if (playerObj == null)
            return;

        Transform visualEarly = playerObj.transform.Find("PlayerAvatarVisual");
        if (visualEarly != null)
            EnsureAnimatorDriverOnModelRoot(visualEarly.gameObject);

        CalemOpenWorldAnimatorBootstrap boot = playerObj.GetComponent<CalemOpenWorldAnimatorBootstrap>();
        if (boot == null)
            boot = playerObj.AddComponent<CalemOpenWorldAnimatorBootstrap>();

        // Always prefer session / OpenWorld controller so a misassigned demo AnimatorController on the Character prefab cannot win.
        if (OpenWorldPlayerAnimatorOverride != null)
            boot.CalemController = OpenWorldPlayerAnimatorOverride;
        else
        {
            RuntimeAnimatorController loaded =
                Resources.Load<RuntimeAnimatorController>(DefaultOpenWorldAnimatorResourcesPath);
            if (loaded != null)
                boot.CalemController = loaded;
        }

        if (boot.CalemController == null && !s_loggedMissingOpenWorldAnimatorController)
        {
            s_loggedMissingOpenWorldAnimatorController = true;
            Debug.LogWarning(
                "[OpenWorldEncounterManager] Calem locomotion controller is missing. Assign Open World Session → " +
                "Open World Player Animator Override, or ensure " +
                $"Resources.Load can find \"{DefaultOpenWorldAnimatorResourcesPath}\" " +
                "(expected under a folder named Resources, e.g. GameContent/Resources/OpenWorld/CalemOpenWorld.controller).");
        }

        if (refreshRigHierarchy)
            boot.RefreshRigFromHierarchy();
        else
            boot.ApplyControllerIfReady();
    }

    public void AttachPlayerAvatarIfNeeded(GameObject playerObj)
    {
        if (playerObj == null || string.IsNullOrWhiteSpace(PlayerAvatarResourcePath))
            return;

        GameObject avatarPrefab = Resources.Load<GameObject>(PlayerAvatarResourcePath);
        if (avatarPrefab == null)
        {
            Debug.LogWarning($"[OpenWorldEncounterManager] Missing avatar at Resources/{PlayerAvatarResourcePath}");
            return;
        }

        // Scene setups often leave a manual "Calem" hierarchy under Character; runtime also spawns PlayerAvatarVisual.
        // Remove the stale duplicate so only one visual + rig exists (see RemoveLegacyDuplicateAvatarChildren).
        RemoveLegacyDuplicateAvatarChildren(playerObj.transform);

        Transform avatarAnchor = playerObj.transform.Find("PlayerAvatarVisual");
        if (avatarAnchor != null)
        {
            Destroy(avatarAnchor.gameObject);
            avatarAnchor = null;
        }

        Renderer[] existingRenderers = playerObj.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < existingRenderers.Length; i++)
        {
            Renderer renderer = existingRenderers[i];
            if (renderer != null)
                renderer.enabled = false;
        }

        GameObject avatar = Instantiate(avatarPrefab, playerObj.transform);
        avatar.name = "PlayerAvatarVisual";
        avatar.transform.localPosition = PlayerAvatarLocalPosition;
        avatar.transform.localRotation = Quaternion.Euler(PlayerAvatarLocalEuler);
        avatar.transform.localScale = PlayerAvatarLocalScale;

        EnsureAnimatorDriverOnModelRoot(avatar);
    }

    /// <summary>
    /// Deletes direct children that duplicate the runtime-spawned visual (e.g. stray <c>Calem</c> under <c>Character</c>).
    /// </summary>
    static void RemoveLegacyDuplicateAvatarChildren(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        var toDestroy = new List<Transform>(4);
        for (int i = 0; i < playerRoot.childCount; i++)
        {
            Transform c = playerRoot.GetChild(i);
            if (c == null)
                continue;
            if (string.Equals(c.name, "PlayerAvatarVisual", System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(c.name, "Calem", System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (c.GetComponent<CharacterController>() != null)
                continue;

            toDestroy.Add(c);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            if (toDestroy[i] != null)
                Object.Destroy(toDestroy[i].gameObject);
        }
    }

    /// <summary>
    /// FBX-based visual prefabs often ship without an <see cref="Animator"/>; bootstrap code cannot bind until one exists.
    /// </summary>
    void EnsureAnimatorDriverOnModelRoot(GameObject modelRoot)
    {
        if (modelRoot == null)
            return;

        // Prefer an Animator on the visual root; otherwise any child (skinned mesh setups).
        Animator animator = modelRoot.GetComponent<Animator>();
        if (animator == null)
            animator = modelRoot.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = modelRoot.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Open-world rigs often ship as generic/non-humanoid FBX where Resources never exposes a loadable Avatar.
        // Prefer imported avatars when present; otherwise build a generic Avatar from this live hierarchy (runtime).
        if (animator.avatar == null)
        {
            Avatar avatar = TryResolveAvatarForVisual(animator, modelRoot, PlayerAvatarResourcePath);
            if (avatar != null)
                animator.avatar = avatar;
            else if (Application.isPlaying)
                Debug.LogWarning(
                    "[OpenWorldEncounterManager] Could not resolve Avatar for the visual rig (Resources + BuildGenericAvatar). " +
                    "Visual Resources path: \"" + PlayerAvatarResourcePath + "\".",
                    modelRoot);
        }
    }

    /// <summary>
    /// Resolves an <see cref="Avatar"/> for the spawned visual: optional Resources Avatars, then prefab Animator.avatar,
    /// then <see cref="AvatarBuilder.BuildGenericAvatar"/> on the instantiated hierarchy (reliable for generic rigs).
    /// </summary>
    static Avatar TryResolveAvatarForVisual(Animator animator, GameObject modelRoot, string visualPrefabResourcesPath)
    {
        if (modelRoot == null)
            return null;

        foreach (string folder in BuildResourcesFolderCandidates(visualPrefabResourcesPath))
        {
            Avatar fromFolder = TryLoadFirstAvatarFromResourcesFolder(folder);
            if (fromFolder != null)
                return fromFolder;
        }

        if (!string.IsNullOrWhiteSpace(visualPrefabResourcesPath))
        {
            GameObject prefab = Resources.Load<GameObject>(visualPrefabResourcesPath.Trim());
            if (prefab != null)
            {
                foreach (Animator a in prefab.GetComponentsInChildren<Animator>(true))
                {
                    if (a != null && a.avatar != null)
                        return a.avatar;
                }
            }
        }

        try
        {
            Avatar built = BuildGenericAvatarForRuntimeRig(modelRoot);
            if (built != null)
                return built;
        }
        catch (System.Exception)
        {
        }

        if (animator != null && animator.gameObject != modelRoot)
        {
            try
            {
                Avatar builtOnRig = BuildGenericAvatarForRuntimeRig(animator.gameObject);
                if (builtOnRig != null)
                    return builtOnRig;
            }
            catch (System.Exception)
            {
            }
        }

        return null;
    }

    /// <summary>
    /// Unity 6+ <see cref="AvatarBuilder.BuildGenericAvatar(GameObject,string)"/> requires a root-motion bone name; use empty when unused.
    /// </summary>
    static Avatar BuildGenericAvatarForRuntimeRig(GameObject rigRoot)
    {
        if (rigRoot == null)
            return null;
        return AvatarBuilder.BuildGenericAvatar(rigRoot, string.Empty);
    }

    static List<string> BuildResourcesFolderCandidates(string visualPrefabResourcesPath)
    {
        var list = new List<string>(4);
        void Add(string p)
        {
            if (string.IsNullOrWhiteSpace(p))
                return;
            p = p.Trim();
            if (!list.Contains(p))
                list.Add(p);
        }

        if (!string.IsNullOrWhiteSpace(visualPrefabResourcesPath))
        {
            string t = visualPrefabResourcesPath.Trim();
            int slash = t.LastIndexOf('/');
            if (slash > 0)
                Add(t.Substring(0, slash));
        }

        Add("Character/Calem");
        return list;
    }

    static Avatar TryLoadFirstAvatarFromResourcesFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        Object[] typed = Resources.LoadAll(folderPath.Trim(), typeof(Avatar));
        if (typed != null)
        {
            for (int i = 0; i < typed.Length; i++)
            {
                if (typed[i] is Avatar av)
                    return av;
            }
        }

        return null;
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

    void ClaimOwnership(PokemonInstance pokemon)
    {
        if (pokemon == null)
            return;

        pokemon.OriginalTrainerName = PlayerTrainerName;
        pokemon.OriginalTrainerID = PlayerTrainerID;
    }

    static void RegisterDexNumber(List<int> list, PokemonData species)
    {
        if (list == null || species == null || species.DexNumber <= 0)
            return;

        if (!list.Contains(species.DexNumber))
            list.Add(species.DexNumber);
    }

    BagItemStack FindBagStack(string normalizedItemId)
    {
        if (_bagItems == null)
            return null;

        for (int i = 0; i < _bagItems.Count; i++)
        {
            BagItemStack stack = _bagItems[i];
            if (stack == null)
                continue;

            if (NormalizeItemId(stack.ItemId) == normalizedItemId)
                return stack;
        }

        return null;
    }

    static string NormalizeItemId(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? string.Empty
            : itemId.Trim().ToLowerInvariant();
    }

    static bool CanLoadScene(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
