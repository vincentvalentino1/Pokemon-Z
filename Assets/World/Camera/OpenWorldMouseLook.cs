using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Third-person orbit camera (Legends-style): mouse steers yaw/pitch around the player,
/// movement uses a separate yaw-only transform so WASD matches camera heading.
/// Set <see cref="ThirdPerson"/> to false for legacy FPS (eye offset + body yaw).
/// </summary>
public class OpenWorldMouseLook : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("When true, camera orbits behind/above the player. When false, FPS eye offset + body yaw.")]
    public bool ThirdPerson = true;

    [Header("References")]
    [Tooltip("Receives horizontal (yaw) rotation in FPS mode. In third person, orbit yaw is separate.")]
    public Transform PlayerBody;

    [Header("Third person orbit")]
    [Tooltip("How far back the camera sits after yaw/pitch (world-style offset along local -Z).")]
    public float OrbitDistance = 5.4f;
    [Tooltip("Vertical component of the orbit stick before yaw/pitch (higher = more above the player, Pokémon-Z-like).")]
    public float OrbitVerticalLift = 2.15f;
    [Tooltip("Height of the look-at point on the character.")]
    public float LookAtHeight = 1.25f;

    [Header("FPS / detached camera (ThirdPerson = false)")]
    [Tooltip("Eye position in the player's local space when the camera is not a child of the player.")]
    public Vector3 FirstPersonEyeOffsetLocal = new Vector3(0f, 1.6f, 0f);

    [Header("Sensitivity")]
    public float MouseSensitivity = 2f;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Extra scale for the new Input System mouse delta (pixels per frame).")]
    public float NewInputDeltaScale = 0.05f;
#endif

    [Header("Pitch limits")]
    public float MinPitch = 12f;
    public float MaxPitch = 52f;
    [Header("Pitch limits (FPS mode)")]
    public float FpsMinPitch = -85f;
    public float FpsMaxPitch = 85f;

    [Header("Cursor")]
    public bool LockCursorOnPlay = true;
    public KeyCode ToggleCursorKey = KeyCode.Escape;

    float _pitch;
    float _orbitYaw;
    Transform _moveFacingRoot;
    bool _initializedOrbitYaw;

    /// <summary>World-space yaw-only transform used by <see cref="OpenWorldPlayerController"/> for move direction.</summary>
    public Transform MoveFacingRoot => _moveFacingRoot;

    void Awake()
    {
        if (PlayerBody == null && transform.parent != null)
            PlayerBody = transform.parent;

        if (PlayerBody == null && OpenWorldSceneLookup.TryGetPlayerTransform(out Transform playerTransform))
            PlayerBody = playerTransform;

        if (LockCursorOnPlay)
            SetLockedCursor(true);
    }

    void Start()
    {
        if (PlayerBody == null)
            return;

        if (ThirdPerson)
        {
            EnsureMoveFacingRoot();
            if (!_initializedOrbitYaw)
            {
                _orbitYaw = PlayerBody.eulerAngles.y;
                _initializedOrbitYaw = true;
            }

            if (!IsTransformUnder(PlayerBody, transform))
            {
                Quaternion relative = Quaternion.Inverse(PlayerBody.rotation) * transform.rotation;
                _pitch = Mathf.Clamp(NormalizeEulerPitch(relative.eulerAngles.x), MinPitch, MaxPitch);
            }
            else
                _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
        }
        else if (!IsTransformUnder(PlayerBody, transform))
        {
            Quaternion relative = Quaternion.Inverse(PlayerBody.rotation) * transform.rotation;
            _pitch = NormalizeEulerPitch(relative.eulerAngles.x);
            ApplyDetachedCameraPose();
        }
    }

    void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (_moveFacingRoot != null)
        {
            Destroy(_moveFacingRoot.gameObject);
            _moveFacingRoot = null;
        }
    }

    void Update()
    {
        if (OpenWorldPauseMenu.IsOpen)
            return;

        if (ToggleCursorKey != KeyCode.None && WasTogglePressed())
            SetLockedCursor(Cursor.lockState != CursorLockMode.Locked);

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame && !OpenWorldEncounterPromptUI.IsOpen)
            SetLockedCursor(true);
#endif
    }

    void LateUpdate()
    {
        if (PlayerBody == null)
            return;

        if (OpenWorldPauseMenu.IsOpen)
            return;

        if (ThirdPerson)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                ReadMouseDelta(out float mx, out float my);
                if (!Mathf.Approximately(mx, 0f) || !Mathf.Approximately(my, 0f))
                {
                    _orbitYaw += mx;
                    _pitch -= my;
                    _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
                }
            }

            UpdateMoveFacingRoot();
            ApplyThirdPersonCameraPose();
            return;
        }

        bool detached = !IsTransformUnder(PlayerBody, transform);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ReadMouseDelta(out float mx, out float my);
            if (!Mathf.Approximately(mx, 0f) || !Mathf.Approximately(my, 0f))
            {
                PlayerBody.Rotate(0f, mx, 0f, Space.World);
                _pitch -= my;
                _pitch = Mathf.Clamp(_pitch, FpsMinPitch, FpsMaxPitch);
            }
        }

        if (detached)
            ApplyDetachedCameraPose();
        else if (Cursor.lockState == CursorLockMode.Locked)
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    /// <summary>Wired from <see cref="OpenWorldEncounterManager"/> so move facing exists before the first frame.</summary>
    public void BindPlayerForThirdPerson(Transform playerTransform)
    {
        if (playerTransform == null || !ThirdPerson)
            return;

        PlayerBody = playerTransform;
        EnsureMoveFacingRoot();
        UpdateMoveFacingRoot();

        OpenWorldPlayerController pc = playerTransform.GetComponent<OpenWorldPlayerController>();
        if (pc != null)
            pc.CameraTransform = _moveFacingRoot;

        if (!_initializedOrbitYaw)
        {
            _orbitYaw = PlayerBody.eulerAngles.y;
            _initializedOrbitYaw = true;
        }
    }

    void EnsureMoveFacingRoot()
    {
        if (_moveFacingRoot != null)
            return;

        GameObject go = new GameObject("OpenWorld_MoveFacing");
        _moveFacingRoot = go.transform;
    }

    void UpdateMoveFacingRoot()
    {
        if (_moveFacingRoot == null)
            return;

        _moveFacingRoot.position = PlayerBody.position;
        _moveFacingRoot.rotation = Quaternion.Euler(0f, _orbitYaw, 0f);
    }

    void ApplyThirdPersonCameraPose()
    {
        Quaternion orbitRot = Quaternion.Euler(_pitch, _orbitYaw, 0f);
        Vector3 localStick = new Vector3(0f, OrbitVerticalLift, -OrbitDistance);

        transform.position = PlayerBody.position + orbitRot * localStick;
        Vector3 lookTarget = PlayerBody.position + Vector3.up * LookAtHeight;
        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
    }

    void ApplyDetachedCameraPose()
    {
        transform.position = PlayerBody.position + PlayerBody.TransformDirection(FirstPersonEyeOffsetLocal);
        transform.rotation = PlayerBody.rotation * Quaternion.Euler(_pitch, 0f, 0f);
    }

    static bool IsTransformUnder(Transform root, Transform candidate)
    {
        if (root == null || candidate == null)
            return false;

        Transform t = candidate;
        while (t != null)
        {
            if (t == root)
                return true;
            t = t.parent;
        }

        return false;
    }

    static float NormalizeEulerPitch(float eulerX)
    {
        if (eulerX > 180f)
            eulerX -= 360f;
        return eulerX;
    }

    void ReadMouseDelta(out float mx, out float my)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            mx = my = 0f;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        float scale = MouseSensitivity * NewInputDeltaScale;
        mx = delta.x * scale;
        my = delta.y * scale;
#elif ENABLE_LEGACY_INPUT_MANAGER
        mx = Input.GetAxis("Mouse X") * MouseSensitivity;
        my = Input.GetAxis("Mouse Y") * MouseSensitivity;
#else
        mx = my = 0f;
#endif
    }

    bool WasTogglePressed()
    {
        if (ToggleCursorKey == KeyCode.Escape)
            return false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && ToggleCursorKey == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(ToggleCursorKey);
#else
        return false;
#endif
    }

    void SetLockedCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!LockCursorOnPlay || !hasFocus || OpenWorldPauseMenu.IsOpen)
            return;

        SetLockedCursor(true);
    }
}
