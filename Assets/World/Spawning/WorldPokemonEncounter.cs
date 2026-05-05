using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class WorldPokemonEncounter : MonoBehaviour
{
    public WorldPokemonRuntimeData RuntimeData { get; private set; }

    [Header("Interaction")]
    [Tooltip("Maximum click distance from camera.")]
    public float InteractDistance = 20f;
    [Tooltip("Player must be within this world distance to open Battle/Cancel prompt.")]
    public float RequiredPlayerDistance = 4.5f;

    [Header("Selection Highlight")]
    [Tooltip("Changes outline/tint color while this wild Pokemon is under center-screen aim and in interact range.")]
    public bool ShowSelectionHighlight = true;
    public Color SelectedColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color OutOfRangeColor = new Color(1f, 0.45f, 0.2f, 1f);

    public bool IsUsed { get; private set; }

    Collider _collider;
    Transform _player;
    Renderer[] _renderers;
    MaterialPropertyBlock _mpb;
    bool _isHighlighted;
    bool _highlightInRange;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    public void Initialize(WorldPokemonRuntimeData data)
    {
        RuntimeData = data;
    }

    void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (IsUsed || OpenWorldEncounterManager.Instance == null || RuntimeData == null)
        {
            SetHighlighted(false);
            return;
        }

        if (OpenWorldEncounterPromptUI.IsOpen || OpenWorldPauseMenu.IsOpen)
        {
            SetHighlighted(false);
            return;
        }

        bool isSelected = IsSelectedByCenterAim(out float hitDistance);
        bool canInteract = isSelected && IsWithinInteractDistance(hitDistance) && IsPlayerCloseEnough();
        SetHighlighted(isSelected, canInteract);
    }

    public void BeginForcedEncounter()
    {
        TriggerEncounter();
    }

    public void BeginEncounterFromPrompt()
    {
        TriggerEncounter();
    }

    void TriggerEncounter()
    {
        if (IsUsed || OpenWorldEncounterManager.Instance == null || RuntimeData == null)
            return;

        IsUsed = true;
        OpenWorldEncounterManager.Instance.StartWildEncounter(RuntimeData.Instance, transform.position, gameObject, RuntimeData.DestroyAfterEncounter);
    }

    void OnDisable()
    {
        SetHighlighted(false);
    }

    void OnDestroy()
    {
        SetHighlighted(false);
    }

    bool IsSelectedByCenterAim(out float hitDistance)
    {
        hitDistance = 0f;
        if (!TryGetEncounterUnderCenter(out WorldPokemonEncounter focused, out hitDistance))
            return false;

        return focused == this;
    }

    bool IsWithinInteractDistance(float hitDistance)
    {
        if (InteractDistance <= 0f)
            return true;

        return hitDistance <= InteractDistance;
    }

    bool IsPlayerCloseEnough()
    {
        if (RequiredPlayerDistance <= 0f)
            return true;

        ResolvePlayer();
        if (_player == null)
            return true;

        Vector3 delta = _player.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= RequiredPlayerDistance * RequiredPlayerDistance;
    }

    void ResolvePlayer()
    {
        if (_player != null)
            return;

        if (OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
            _player = playerTransform;
    }

    static bool TryGetCenterScreenRay(out Ray ray)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            ray = default;
            return false;
        }

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        ray = cam.ScreenPointToRay(center);
        return true;
    }

    static bool TryGetEncounterUnderCenter(out WorldPokemonEncounter encounter, out float hitDistance)
    {
        encounter = null;
        hitDistance = 0f;
        if (!TryGetCenterScreenRay(out Ray ray))
            return false;

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            return false;

        Collider c = hit.collider;
        if (c == null)
            return false;

        encounter = c.GetComponentInParent<WorldPokemonEncounter>();
        hitDistance = hit.distance;
        return encounter != null;
    }

    void SetHighlighted(bool highlighted, bool inRange = true)
    {
        if (!ShowSelectionHighlight)
            highlighted = false;

        if (_isHighlighted == highlighted && _highlightInRange == inRange)
            return;

        _isHighlighted = highlighted;
        _highlightInRange = inRange;
        if (_renderers == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null)
                continue;

            if (!highlighted)
            {
                r.SetPropertyBlock(null);
                continue;
            }

            Material mat = r.sharedMaterial;
            if (mat == null)
                continue;

            _mpb.Clear();
            r.GetPropertyBlock(_mpb);
            Color tint = inRange ? SelectedColor : OutOfRangeColor;
            if (mat.HasProperty(OutlineColorId))
                _mpb.SetColor(OutlineColorId, tint);
            else if (mat.HasProperty(BaseColorId))
                _mpb.SetColor(BaseColorId, tint);
            else if (mat.HasProperty(ColorId))
                _mpb.SetColor(ColorId, tint);
            else
                continue;

            r.SetPropertyBlock(_mpb);
        }
    }
}
