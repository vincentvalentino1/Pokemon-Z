using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// How this zone refills wild Pokémon after the initial placement.
/// </summary>
public enum WorldSpawnRefillMode
{
    /// <summary>Spawn the full cap once when the overworld finishes booting; no timed respawns until you return from battle or enter the scene again.</summary>
    BatchOncePerVisit = 0,
    /// <summary>Coroutine periodically tops up wilds up to the cap (legacy feel).</summary>
    Continuous = 1
}

public class WorldSpawnArea : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("Legends-style: only one overworld wild at a time, no wandering, battle then respawn one new wild.")]
    public bool SingleWildPokemonMode = true;
    [Tooltip("When Single Wild Mode is on: share one wild across the whole world with other spawn areas that also enable this. Turn off to allow one wild per zone (each area independent).")]
    public bool ParticipateInGlobalWildCap = true;
    public float Radius = 24f;
    [Tooltip("Ignored when Single Wild Mode is on (always one).")]
    public int MaxAlivePokemon = 6;
    [Tooltip("BatchOncePerVisit: spawn this many wilds in one pass (still 1 when SingleWild mode). Continuous: ignored until periodic top-up.")]
    public WorldSpawnRefillMode RefillMode = WorldSpawnRefillMode.BatchOncePerVisit;
    [Tooltip("Continuous mode only — seconds between top-up attempts.")]
    public float SpawnInterval = 2.5f;
    public float HeightOffset = 0.5f;
    [Tooltip("When Single Wild Mode is on, far despawn is disabled so your only encounter is not removed.")]
    public float DespawnDistanceFromPlayer = 45f;
    [Tooltip("In single-wild mode, spawn around the player so the encounter is easy to see.")]
    public bool SpawnNearPlayerInSingleMode = true;
    [Tooltip("Outer radius (meters) around the spawn center for player-near mode. Inner radius is at least MinDistanceFromPlayer.")]
    public float SingleModePlayerSpawnRadius = 22f;
    
    [Header("Spawn Distribution")]
    [Tooltip("Lets overworld spawns follow where the player is currently moving instead of always using the SpawnArea transform as the center.")]
    public bool FollowPlayerMovementForSpawns = true;
    [Tooltip("How far the player must travel before new wilds start spawning around the new location.")]
    public float PlayerTravelBeforeRecenter = 16f;
    [Tooltip("Hard minimum horizontal distance from the player; candidates closer than this are rejected.")]
    public float MinDistanceFromPlayer = 12f;
    [Tooltip("Hard minimum horizontal distance between two active wilds.")]
    public float MinDistanceBetweenWilds = 14f;
    [Range(1, 40)]
    [Tooltip("How many possible positions to test before choosing the best spawn point.")]
    public int SpawnCandidateSamples = 18;

    [Header("Sight Masking")]
    [Tooltip("Strongly prefers spawn points that are outside the player's current view so Pokemon do not pop into existence on screen.")]
    public bool AvoidSpawningInPlayerSight = true;
    [Tooltip("How much to penalize candidates that are directly visible to the main camera.")]
    public float VisibleSpawnPenalty = 250f;
    [Tooltip("How much to penalize candidates that are in front of the player, even if no camera visibility check is available.")]
    public float FrontConeSpawnPenalty = 90f;
    [Range(-1f, 1f)]
    [Tooltip("Dot threshold used for the fallback player-facing cone check. Higher = stricter front-only check.")]
    public float PlayerSightDotThreshold = 0.15f;
    [Tooltip("Raises the candidate point for visibility checks so tall grass or slopes do not count as hidden too easily.")]
    public float SightCheckHeight = 1.1f;
    public LayerMask GroundLayer = ~0;

    [Header("Spawn Timing")]
    [Tooltip("After a successful spawn, wait this long before trying again. Bypassed for the first spawn and for post-battle respawns.")]
    public float MinTimeBetweenSpawns = 4f;

    [Header("Debug")]
    public bool VerboseLogging = false;

    [Header("Spawn Profile")]
    public WorldSpawnProfile SpawnProfile;

    readonly List<GameObject> _alive = new List<GameObject>();
    float _nextSpawnAllowedTime;
    Transform _player;
    Vector3 _spawnFocus;
    bool _hasSpawnFocus;

    void OnEnable()
    {
        if (RefillMode == WorldSpawnRefillMode.Continuous)
            StartCoroutine(SpawnRoutine());
    }

    bool HasValidSpawnProfile()
    {
        if (SpawnProfile == null || SpawnProfile.SpawnTable == null || SpawnProfile.SpawnTable.Length == 0)
            return false;

        for (int i = 0; i < SpawnProfile.SpawnTable.Length; i++)
        {
            if (SpawnProfile.SpawnTable[i] != null && SpawnProfile.SpawnTable[i].Species != null)
                return true;
        }

        return false;
    }

    IEnumerator SpawnRoutine()
    {
        yield return null;

        while (true)
        {
            ResolvePlayer();
            CleanupMissing();
            if (!SingleWildPokemonMode)
                CleanupFarFromPlayer();

            int cap = SingleWildPokemonMode ? 1 : Mathf.Max(1, MaxAlivePokemon);
            if (_alive.Count < cap)
                TrySpawnOne(ignoreSpawnCooldown: false);

            yield return new WaitForSeconds(SpawnInterval);
        }
    }

    /// <summary>
    /// Called when the overworld scene has finished booting (player + camera ready) or when returning from battle.
    /// Brings every zone up to its wild cap in one pass. Safe to call multiple times.
    /// </summary>
    public static void TopUpAllAreasAfterWorldReady()
    {
        WorldSpawnArea[] areas = Object.FindObjectsByType<WorldSpawnArea>(FindObjectsSortMode.None);
        for (int i = 0; i < areas.Length; i++)
        {
            WorldSpawnArea a = areas[i];
            if (a == null || !a.isActiveAndEnabled)
                continue;

            a.CleanupMissing();
            if (!a.HasValidSpawnProfile())
                continue;

            a.FillToCap(ignoreSpawnCooldown: true);
        }
    }

    /// <summary>Obsolete: use <see cref="TopUpAllAreasAfterWorldReady"/>.</summary>
    public static void RespawnSingleWildIfNeeded() => TopUpAllAreasAfterWorldReady();

    public void FillToCap(bool ignoreSpawnCooldown)
    {
        int cap = SingleWildPokemonMode ? 1 : Mathf.Max(1, MaxAlivePokemon);
        int maxPasses = cap * 12 + 8;
        int pass = 0;

        while (_alive.Count < cap && pass++ < maxPasses)
        {
            int before = _alive.Count;
            TrySpawnOne(ignoreSpawnCooldown);
            if (_alive.Count == before)
                break;
        }
    }

    public void TrySpawnOne(bool ignoreSpawnCooldown = false)
    {
        if (!ignoreSpawnCooldown && MinTimeBetweenSpawns > 0f && Time.time < _nextSpawnAllowedTime)
            return;

        if (!HasValidSpawnProfile())
        {
            Debug.LogWarning("[WorldSpawnArea] Cannot spawn: SpawnProfile is missing or empty! Please create a WorldSpawnProfile and assign it to the SpawnArea.");
            return;
        }

        if (SingleWildPokemonMode && ParticipateInGlobalWildCap && !WorldWildSpawnCoordinator.CanSpawnWild())
            return;

        SpawnProfileEntry picked = PickEntry();
        if (picked == null || picked.Species == null)
        {
            Debug.LogWarning("[WorldSpawnArea] Cannot spawn: Picked entry is null or has no Species assigned!");
            return;
        }

        if (!TryGetSpawnPosition(out Vector3 spawnPos))
        {
            if (VerboseLogging)
                Debug.Log("[WorldSpawnArea] No spawn position passed distance checks; will retry later.");
            return;
        }

        if (VerboseLogging)
            Debug.Log($"[WorldSpawnArea] Attempting to spawn {picked.Species.PokemonName} at {spawnPos}");
        
        GameObject spawned = SpawnWorldPokemonVisual(picked, spawnPos);
        if (spawned == null)
        {
            Debug.LogError("[WorldSpawnArea] SpawnWorldPokemonVisual returned null!");
            return;
        }

        EnsureEncounterCollider(spawned);

        WorldPokemonEncounter encounter = spawned.GetComponent<WorldPokemonEncounter>();
        if (encounter == null)
            encounter = spawned.AddComponent<WorldPokemonEncounter>();

        // Create Runtime Data matching the spec
        int level = Random.Range(Mathf.Min(picked.MinLevel, picked.MaxLevel), Mathf.Max(picked.MinLevel, picked.MaxLevel) + 1);
        bool destroyAfter = SingleWildPokemonMode; // True if legends style singleton
        
        WorldPokemonRuntimeData runtimeData = PokemonFactory.CreateRuntimeWorldPokemon(picked.Species, level, picked.BehaviorProfile, destroyAfter);
        encounter.Initialize(runtimeData);

        // Always add AI so wild Pokemon can chase/engage
        WorldPokemonAI ai = spawned.GetComponent<WorldPokemonAI>();
        if (ai == null)
            ai = spawned.AddComponent<WorldPokemonAI>();
        ai.GroundLayer = GroundLayer;

        _alive.Add(spawned);
        if (VerboseLogging)
            Debug.Log($"[WorldSpawnArea] Successfully spawned {picked.Species.PokemonName} ({spawned.name}). Alive count: {_alive.Count}");

        if (MinTimeBetweenSpawns > 0f)
            _nextSpawnAllowedTime = Time.time + MinTimeBetweenSpawns;

        if (SingleWildPokemonMode && ParticipateInGlobalWildCap)
            WorldWildSpawnCoordinator.Register(spawned);
    }

    GameObject SpawnWorldPokemonVisual(SpawnProfileEntry entry, Vector3 spawnPos)
    {
        GameObject prefab = entry.SpawnPrefabOverride;
        if (prefab == null && entry.Species != null && !string.IsNullOrWhiteSpace(entry.Species.PokemonName))
            prefab = Resources.Load<GameObject>("Pokemon/" + entry.Species.PokemonName);

        if (prefab != null)
            return Instantiate(prefab, spawnPos, Quaternion.identity, transform);

        // Fallback for quick testing when no prefab is assigned.
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        fallback.name = "WildPokemon_Fallback";
        fallback.transform.SetPositionAndRotation(spawnPos + Vector3.up * 0.35f, Quaternion.identity);
        fallback.transform.SetParent(transform);
        fallback.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);

        Collider col = fallback.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        Renderer renderer = fallback.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.4f, 0.85f, 0.3f);

        return fallback;
    }

    static void EnsureEncounterCollider(GameObject spawned)
    {
        if (spawned == null)
            return;

        Collider rootCollider = spawned.GetComponent<Collider>();
        if (rootCollider == null)
        {
            SphereCollider sphere = spawned.AddComponent<SphereCollider>();
            ConfigureSphereCollider(spawned, sphere);
            rootCollider = sphere;
        }

        rootCollider.isTrigger = true;
    }

    static void ConfigureSphereCollider(GameObject spawned, SphereCollider sphere)
    {
        Renderer[] renderers = spawned.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            sphere.center = Vector3.up * 0.75f;
            sphere.radius = 0.75f;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        sphere.center = spawned.transform.InverseTransformPoint(bounds.center);
        Vector3 extents = bounds.extents;
        sphere.radius = Mathf.Max(0.5f, Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)));
    }

    SpawnProfileEntry PickEntry()
    {
        if (SpawnProfile == null || SpawnProfile.SpawnTable == null) return null;
        
        var spawnTable = SpawnProfile.SpawnTable;
        int totalWeight = 0;
        for (int i = 0; i < spawnTable.Length; i++)
        {
            SpawnProfileEntry entry = spawnTable[i];
            if (entry == null || entry.Species == null)
                continue;

            totalWeight += Mathf.Max(0, entry.Weight);
        }

        if (totalWeight <= 0)
            return PickRandomSpeciesEntryIgnoringWeights(spawnTable);

        int roll = Random.Range(0, totalWeight);
        int running = 0;
        for (int i = 0; i < spawnTable.Length; i++)
        {
            SpawnProfileEntry entry = spawnTable[i];
            if (entry == null || entry.Species == null)
                continue;

            running += Mathf.Max(0, entry.Weight);
            if (roll < running)
                return entry;
        }

        return PickRandomSpeciesEntryIgnoringWeights(spawnTable);
    }

    SpawnProfileEntry PickRandomSpeciesEntryIgnoringWeights(SpawnProfileEntry[] table)
    {
        int valid = 0;
        for (int i = 0; i < table.Length; i++)
        {
            if (table[i] != null && table[i].Species != null)
                valid++;
        }

        if (valid == 0)
            return null;

        int pick = Random.Range(0, valid);
        for (int i = 0; i < table.Length; i++)
        {
            SpawnProfileEntry entry = table[i];
            if (entry == null || entry.Species == null)
                continue;

            if (pick == 0)
                return entry;
            pick--;
        }

        return null;
    }

    bool TryGetSpawnPosition(out Vector3 spawnPos)
    {
        ResolvePlayer();
        Vector3 center = ResolveSpawnCenter();
        float radius = ResolveSpawnRadius();

        if (VerboseLogging)
            Debug.Log($"[WorldSpawnArea] Spawn pick center {center}, outer radius {radius}");

        return TryEvaluateSpawnAround(center, radius, out spawnPos);
    }

    bool TryEvaluateSpawnAround(Vector3 center, float radius, out Vector3 bestCandidate)
    {
        int attempts = Mathf.Max(8, SpawnCandidateSamples);
        bestCandidate = center + Vector3.up * (HeightOffset + 0.75f);
        float bestScore = float.NegativeInfinity;
        bool anyValid = false;

        bool useAnnulus = _player != null && (
            SingleWildPokemonMode && SpawnNearPlayerInSingleMode ||
            (!SingleWildPokemonMode && FollowPlayerMovementForSpawns));

        float innerDisk = useAnnulus ? Mathf.Max(1f, MinDistanceFromPlayer) : 0f;
        float outerDisk = useAnnulus
            ? Mathf.Max(innerDisk + 3f, Mathf.Max(1f, radius))
            : Mathf.Max(1f, radius);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 circle = useAnnulus
                ? SampleAnnulusFlat(innerDisk, outerDisk)
                : Random.insideUnitCircle * outerDisk;

            Vector3 sampleOrigin = center + new Vector3(circle.x, 200f, circle.y);
            Vector3 candidate = center + new Vector3(circle.x, HeightOffset + 0.75f, circle.y);
            bool foundGround = TrySampleGround(sampleOrigin, out candidate);

            if (!PassesHardDistanceChecks(candidate))
                continue;

            float score = ScoreSpawnCandidate(candidate);
            if (foundGround)
                score += 3f;

            anyValid = true;
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (!anyValid)
        {
            bestCandidate = default;
            return false;
        }

        return true;
    }

    static Vector2 SampleAnnulusFlat(float innerRadius, float outerRadius)
    {
        outerRadius = Mathf.Max(outerRadius, innerRadius + 0.5f);
        float t = Random.value;
        float r = Mathf.Sqrt(Mathf.Lerp(innerRadius * innerRadius, outerRadius * outerRadius, t));
        float ang = Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
    }

    bool PassesHardDistanceChecks(Vector3 candidate)
    {
        if (_player != null && MinDistanceFromPlayer > 0f)
        {
            float minSq = MinDistanceFromPlayer * MinDistanceFromPlayer;
            if (FlatDistanceSqr(candidate, _player.position) < minSq * 0.99f)
                return false;
        }

        if (MinDistanceBetweenWilds > 0f)
        {
            float wildSq = MinDistanceBetweenWilds * MinDistanceBetweenWilds;
            for (int i = 0; i < _alive.Count; i++)
            {
                GameObject other = _alive[i];
                if (other == null)
                    continue;
                if (FlatDistanceSqr(candidate, other.transform.position) < wildSq * 0.99f)
                    return false;
            }
        }

        return true;
    }

    void CleanupMissing()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
                _alive.RemoveAt(i);
        }
    }

    void CleanupFarFromPlayer()
    {
        if (_player == null || DespawnDistanceFromPlayer <= 0f)
            return;

        float sqrLimit = DespawnDistanceFromPlayer * DespawnDistanceFromPlayer;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            GameObject obj = _alive[i];
            if (obj == null)
                continue;

            Vector3 delta = obj.transform.position - _player.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrLimit)
                continue;

            Destroy(obj);
            _alive.RemoveAt(i);
        }
    }

    void ResolvePlayer()
    {
        if (_player != null)
            return;

        if (OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
            _player = playerTransform;
    }

    Vector3 ResolveSpawnCenter()
    {
        if (_player == null)
            return transform.position;

        if (SingleWildPokemonMode && SpawnNearPlayerInSingleMode)
            return UpdateSpawnFocus(_player.position);

        if (!SingleWildPokemonMode && FollowPlayerMovementForSpawns)
            return UpdateSpawnFocus(_player.position);

        _hasSpawnFocus = false;
        return transform.position;
    }

    float ResolveSpawnRadius()
    {
        if (SingleWildPokemonMode && SpawnNearPlayerInSingleMode)
            return Mathf.Max(1f, SingleModePlayerSpawnRadius);

        return Mathf.Max(1f, Radius);
    }

    Vector3 UpdateSpawnFocus(Vector3 playerPosition)
    {
        Vector3 flattenedPlayer = new Vector3(playerPosition.x, transform.position.y, playerPosition.z);
        if (!_hasSpawnFocus || FlatDistanceSqr(_spawnFocus, flattenedPlayer) >= PlayerTravelBeforeRecenter * PlayerTravelBeforeRecenter)
        {
            _spawnFocus = flattenedPlayer;
            _hasSpawnFocus = true;
        }

        return _spawnFocus;
    }

    bool TrySampleGround(Vector3 origin, out Vector3 point)
    {
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, GroundLayer, QueryTriggerInteraction.Ignore))
        {
            point = hit.point + Vector3.up * HeightOffset;
            return true;
        }

        if (VerboseLogging)
            Debug.LogWarning($"[WorldSpawnArea] No ground detected below {origin}! Spawning in fallback position. Check GroundLayer mask or Terrain scale.");
        point = origin + Vector3.down * (200f - (HeightOffset + 0.75f));
        return false;
    }

    float ScoreSpawnCandidate(Vector3 candidate)
    {
        float score = Random.value * 0.25f;

        if (_player != null)
        {
            float playerDistance = Mathf.Sqrt(FlatDistanceSqr(candidate, _player.position));
            if (playerDistance < MinDistanceFromPlayer)
                score -= (MinDistanceFromPlayer - playerDistance + 1f) * 100f;
            else
                score += Mathf.Clamp(playerDistance - MinDistanceFromPlayer, 0f, MinDistanceFromPlayer * 3f) * 0.35f;
        }

        if (AvoidSpawningInPlayerSight)
            score -= GetSightPenalty(candidate);

        for (int i = 0; i < _alive.Count; i++)
        {
            GameObject other = _alive[i];
            if (other == null)
                continue;

            float otherDistance = Mathf.Sqrt(FlatDistanceSqr(candidate, other.transform.position));
            if (otherDistance < MinDistanceBetweenWilds)
                score -= (MinDistanceBetweenWilds - otherDistance + 1f) * 100f;
            else
                score += Mathf.Min(otherDistance, MinDistanceBetweenWilds);
        }

        return score;
    }

    float GetSightPenalty(Vector3 candidate)
    {
        float penalty = 0f;

        if (IsVisibleToMainCamera(candidate))
            penalty += VisibleSpawnPenalty;

        if (IsInFrontOfPlayer(candidate))
            penalty += FrontConeSpawnPenalty;

        return penalty;
    }

    bool IsVisibleToMainCamera(Vector3 candidate)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 probe = candidate + Vector3.up * Mathf.Max(0f, SightCheckHeight);
        Vector3 viewport = cam.WorldToViewportPoint(probe);
        if (viewport.z <= 0f)
            return false;

        if (viewport.x < 0.02f || viewport.x > 0.98f || viewport.y < 0.02f || viewport.y > 0.98f)
            return false;

        Vector3 camPos = cam.transform.position;
        Vector3 dir = probe - camPos;
        float dist = dir.magnitude;
        if (dist <= 0.01f)
            return true;

        if (Physics.Raycast(camPos, dir.normalized, out RaycastHit hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // If something blocks the ray before it reaches the candidate, treat the point as hidden.
            if (hit.distance < dist - 0.2f)
                return false;
        }

        return true;
    }

    bool IsInFrontOfPlayer(Vector3 candidate)
    {
        if (_player == null)
            return false;

        Vector3 toCandidate = candidate - _player.position;
        toCandidate.y = 0f;
        if (toCandidate.sqrMagnitude <= 0.01f)
            return true;

        Vector3 forward = _player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.01f)
            return false;

        float dot = Vector3.Dot(forward.normalized, toCandidate.normalized);
        return dot >= PlayerSightDotThreshold;
    }

    static bool TryGetPlayerPosition(out Vector3 playerPosition)
    {
        playerPosition = Vector3.zero;
        if (!OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
            return false;

        playerPosition = playerTransform.position;
        return true;
    }

    static float FlatDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
