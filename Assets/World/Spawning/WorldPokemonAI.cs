using UnityEngine;

public enum AIState
{
    Idle,
    Roam,
    Notice,
    Chase,
    Engage,
    Return
}

[RequireComponent(typeof(WorldPokemonEncounter))]
public class WorldPokemonAI : MonoBehaviour
{
    public LayerMask GroundLayer = ~0;
    [Tooltip("Keeps a small gap so feet don't clip into uneven terrain.")]
    public float GroundClearance = 0.02f;

    static WorldPokemonBehaviorProfile _defaultProfile;

    AIState _currentState = AIState.Idle;
    WorldPokemonEncounter _encounter;
    Transform _player;
    Vector3 _spawnOrigin;
    Renderer[] _renderers;

    // Roam state memory
    Vector3 _roamTarget;
    float _stateTimer;

    // Avoid physics issues when spawning slightly underground
    float _timeAlive;

    void Awake()
    {
        _encounter = GetComponent<WorldPokemonEncounter>();
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Start()
    {
        _spawnOrigin = transform.position;
        ResolvePlayer();
        ChangeState(AIState.Idle);
    }

    void Update()
    {
        _timeAlive += Time.deltaTime;

        if (_encounter == null || _encounter.RuntimeData == null)
            return;

        if (_encounter.IsUsed)
            return; // Encounter already triggered, freeze AI

        ResolvePlayer();

        switch (_currentState)
        {
            case AIState.Idle:
                UpdateIdle();
                break;
            case AIState.Roam:
                UpdateRoam();
                break;
            case AIState.Notice:
                UpdateNotice();
                break;
            case AIState.Chase:
                UpdateChase();
                break;
            case AIState.Return:
                UpdateReturn();
                break;
            case AIState.Engage:
                // Handled in ChangeState
                break;
        }

        SnapToGround();

        // Always check bounds if chasing
        if (_currentState == AIState.Chase)
        {
            float distToSpawn = Vector3.Distance(transform.position, _spawnOrigin);
            if (distToSpawn > Profile.LeashDistance)
            {
                ChangeState(AIState.Return);
            }
        }
    }

    WorldPokemonBehaviorProfile Profile => _encounter.RuntimeData.BehaviorProfile != null
        ? _encounter.RuntimeData.BehaviorProfile
        : GetDefaultProfile();

    void ResolvePlayer()
    {
        if (_player != null)
            return;

        if (OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
            _player = playerTransform;
    }

    void ChangeState(AIState newState)
    {
        _currentState = newState;
        _stateTimer = 0f;

        if (newState == AIState.Roam)
        {
            PickRoamTarget();
        }
        else if (newState == AIState.Engage)
        {
            _encounter.BeginForcedEncounter();
        }
    }

    void UpdateIdle()
    {
        _stateTimer += Time.deltaTime;
        if (CheckEngagement()) return;

        if (_stateTimer > 2f)
        {
            ChangeState(AIState.Roam);
        }
    }

    void UpdateRoam()
    {
        if (CheckEngagement()) return;

        Vector3 toTarget = _roamTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.1f)
        {
            ChangeState(AIState.Idle);
            return;
        }

        MoveTowards(toTarget.normalized, Profile.RoamSpeed);
    }

    void UpdateNotice()
    {
        // Stand still and "notice" the player
        _stateTimer += Time.deltaTime;

        if (_player != null)
        {
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            TurnTowards(toPlayer.normalized);
        }

        if (_stateTimer > 1f) // 1 second notice time
        {
            if (Profile.BehaviorType == AIBehaviorType.Aggressive)
            {
                ChangeState(AIState.Chase);
            }
            else if (Profile.BehaviorType == AIBehaviorType.Fleeing)
            {
                // Simple flee: runaway from player
                ChangeState(AIState.Return); // For now, Return acts as flee back to origin, or we could add a Flee target.
            }
            else
            {
                ChangeState(AIState.Idle);
            }
        }
    }

    void UpdateChase()
    {
        if (_player == null)
        {
            ChangeState(AIState.Return);
            return;
        }

        if (CheckEngagement()) return;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;

        // Give up if player gets too far out of notice radius + buffer
        if (toPlayer.magnitude > Profile.NoticeRadius * 1.5f)
        {
            ChangeState(AIState.Return);
            return;
        }

        if (Profile.BehaviorType == AIBehaviorType.Aggressive)
        {
            MoveTowards(toPlayer.normalized, Profile.ChaseSpeed);
        }
    }

    void UpdateReturn()
    {
        Vector3 toSpawn = _spawnOrigin - transform.position;
        toSpawn.y = 0f;

        if (toSpawn.magnitude <= 0.5f)
        {
            ChangeState(AIState.Idle);
            return;
        }

        MoveTowards(toSpawn.normalized, Profile.RoamSpeed);
    }

    bool CheckEngagement()
    {
        if (_player == null || _timeAlive < 1f) return false;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float distSq = toPlayer.sqrMagnitude;

        if (distSq <= Profile.EngageRadius * Profile.EngageRadius)
        {
            ChangeState(AIState.Engage);
            return true;
        }

        if ((_currentState == AIState.Idle || _currentState == AIState.Roam) && 
            Profile.BehaviorType != AIBehaviorType.Passive)
        {
            if (distSq <= Profile.NoticeRadius * Profile.NoticeRadius)
            {
                ChangeState(AIState.Notice);
                return true;
            }
        }

        return false;
    }

    void PickRoamTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * Profile.RoamRadius;
        Vector3 probe = _spawnOrigin + new Vector3(randomCircle.x, 20f, randomCircle.y);

        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 50f, GroundLayer, QueryTriggerInteraction.Ignore))
            _roamTarget = hit.point;
        else
            _roamTarget = _spawnOrigin + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    void MoveTowards(Vector3 dir, float speed)
    {
        if (dir == Vector3.zero) return;
        transform.position += dir * speed * Time.deltaTime;
        TurnTowards(dir);
    }

    void SnapToGround()
    {
        // Cast a robust ray from above to locate the terrain below the Pokemon.
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up * 50f, Vector3.down, 100f, GroundLayer, QueryTriggerInteraction.Ignore);
        float groundY = float.MinValue;
        bool foundGround = false;

        foreach (RaycastHit hit in hits)
        {
            // Ignore the Pokemon's own colliders, and the Player's colliders
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (_player != null && hit.collider.transform.IsChildOf(_player)) continue;

            if (hit.point.y > groundY)
            {
                groundY = hit.point.y;
                foundGround = true;
            }
        }

        if (!foundGround)
            return;

        float visualBottomY = GetVisualBottomY();
        Vector3 pos = transform.position;

        if (!float.IsNaN(visualBottomY))
        {
            pos.y += (groundY + GroundClearance) - visualBottomY;
        }
        else
        {
            pos.y = groundY + GroundClearance;
        }

        transform.position = pos;
    }

    float GetVisualBottomY()
    {
        if (_renderers == null || _renderers.Length == 0)
            return float.NaN;

        float lowestY = float.PositiveInfinity;
        bool foundRenderer = false;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            lowestY = Mathf.Min(lowestY, renderer.bounds.min.y);
            foundRenderer = true;
        }

        return foundRenderer ? lowestY : float.NaN;
    }

    static WorldPokemonBehaviorProfile GetDefaultProfile()
    {
        if (_defaultProfile != null)
            return _defaultProfile;

        _defaultProfile = ScriptableObject.CreateInstance<WorldPokemonBehaviorProfile>();
        _defaultProfile.hideFlags = HideFlags.HideAndDontSave;
        _defaultProfile.BehaviorType = AIBehaviorType.Passive;
        _defaultProfile.NoticeRadius = 8f;
        _defaultProfile.EngageRadius = 1.5f;
        _defaultProfile.RoamSpeed = 1.7f;
        _defaultProfile.ChaseSpeed = 4.5f;
        _defaultProfile.RotationSpeed = 8f;
        _defaultProfile.RoamRadius = 5f;
        _defaultProfile.LeashDistance = 15f;
        _defaultProfile.DisengageTime = 2f;
        return _defaultProfile;
    }

    void TurnTowards(Vector3 dir)
    {
        if (dir == Vector3.zero) return;
        Quaternion wanted = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, Profile.RotationSpeed * Time.deltaTime);
    }
}
