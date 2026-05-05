using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(0)]
[RequireComponent(typeof(CharacterController))]
public class OpenWorldPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float MoveSpeed = 4.5f;
    public float SprintSpeed = 7f;
    [Tooltip("When false, the player does not turn toward move input (use with FPS mouse look).")]
    public bool RotateTowardMoveDirection = true;
    public float RotationSpeed = 10f;
    public float Gravity = -20f;
    public KeyCode SprintKey = KeyCode.LeftShift;

    [Header("Jump")]
    [Tooltip("Approximate peak height in meters when gravity is applied as configured.")]
    public float JumpHeight = 1.15f;
    public KeyCode JumpKey = KeyCode.Space;

    [Header("Ground check")]
    [Tooltip("Extra distance below the capsule foot to probe for ground. Fixes jump on flat floors when CharacterController.isGrounded misses.")]
    public float GroundProbeDistance = 0.22f;
    public LayerMask GroundLayers = Physics.DefaultRaycastLayers;

    [Header("References")]
    public Transform CameraTransform;
    public Animator Animator;

    public bool LocomotionIsMoving { get; private set; }
    public bool LocomotionIsSprinting { get; private set; }

    CharacterController _controller;
    Vector3 _velocity;
    bool _warnedMissingController;
    int _bindAnimatorAttemptFrame;
    const int _maxBindAnimatorFrames = 120;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
            _controller = gameObject.AddComponent<CharacterController>();

        // Ensure movement controller is usable even if scene setup is incomplete.
        _controller.enabled = true;
        if (_controller.height < 0.1f) _controller.height = 2f;
        if (_controller.radius < 0.1f) _controller.radius = 0.4f;
        if (_controller.center == Vector3.zero) _controller.center = new Vector3(0f, 1f, 0f);
        if (_controller.stepOffset <= 0f) _controller.stepOffset = 0.3f;

        if (CameraTransform == null && Camera.main != null)
            CameraTransform = Camera.main.transform;

        if (Animator == null)
            Animator = OpenWorldRigAnimators.FindRigAnimator(transform);
        if (Animator == null)
            RequestBootstrapBind();

        CalemOpenWorldDirectClipDriver direct = GetComponent<CalemOpenWorldDirectClipDriver>();
        if (Animator != null && (direct == null || !direct.IsDirectSampling))
            Animator.enabled = true;
    }

    void Start()
    {
        TryBindAnimatorIfMissing();
    }

    void OnEnable()
    {
        if (Application.isPlaying)
            TryBindAnimatorIfMissing();
    }

    void TryBindAnimatorIfMissing()
    {
        if (Animator == null)
            Animator = OpenWorldRigAnimators.FindRigAnimator(transform);
        if (Animator == null)
        {
            RequestBootstrapBind();
            return;
        }

        CalemOpenWorldDirectClipDriver direct = GetComponent<CalemOpenWorldDirectClipDriver>();

        if (Animator.runtimeAnimatorController == null)
        {
            bool playableDrivesRig = direct != null
                && direct.IsDirectSampling
                && direct.CurrentMode == CalemOpenWorldDirectClipDriver.LocomotionMode.PlayableGraph;
            if (!playableDrivesRig)
                RequestBootstrapBind();
        }

        if ((direct == null || !direct.IsDirectSampling))
            Animator.enabled = true;

        _checkedAnimatorParams = false;
    }

    void RequestBootstrapBind()
    {
        CalemOpenWorldAnimatorBootstrap boot = GetComponent<CalemOpenWorldAnimatorBootstrap>();
        if (boot != null)
            boot.ApplyControllerIfReady();
    }

    public void OnRigAnimatorBound()
    {
        _checkedAnimatorParams = false;
        _lastFallbackStateHash = 0;
    }

    void Update()
    {
        if (Animator == null && _bindAnimatorAttemptFrame < _maxBindAnimatorFrames)
        {
            _bindAnimatorAttemptFrame++;
            TryBindAnimatorIfMissing();
        }

        if (_controller == null)
        {
            if (!_warnedMissingController)
            {
                Debug.LogWarning("[OpenWorldPlayerController] Missing CharacterController on player object.");
                _warnedMissingController = true;
            }
            return;
        }

        bool inputLocked = OpenWorldEncounterPromptUI.IsOpen || OpenWorldPauseMenu.IsOpen;
        Vector2 moveInput = inputLocked ? Vector2.zero : ReadMoveInput();
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        Vector3 moveDirection = ResolveMoveDirection(input);
        bool isMoving = moveDirection.sqrMagnitude > 0.001f;
        bool isSprinting = !inputLocked && isMoving && IsSprintPressed();
        float speed = isSprinting ? SprintSpeed : MoveSpeed;

        if (isMoving && ShouldRotateTowardMovement())
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
        }

        Vector3 horizontalMove = moveDirection * speed;

        bool grounded = IsGrounded();

        if (grounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (grounded && !inputLocked && WasJumpPressed())
            _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);

        _velocity.y += Gravity * Time.deltaTime;

        Vector3 displacement = horizontalMove * Time.deltaTime;
        displacement.y = _velocity.y * Time.deltaTime;
        _controller.Move(displacement);

        LocomotionIsMoving = isMoving;
        LocomotionIsSprinting = isSprinting;

        UpdateAnimator(isMoving, isSprinting, horizontalMove.magnitude);
    }

    bool ShouldRotateTowardMovement()
    {
        if (!RotateTowardMoveDirection)
            return false;

        if (CameraTransform != null && CameraTransform.GetComponent<OpenWorldMouseLook>() != null)
            return false;

        return true;
    }

    Vector3 ResolveMoveDirection(Vector3 input)
    {
        if (input.sqrMagnitude < 0.001f)
            return Vector3.zero;

        if (CameraTransform == null)
            return transform.TransformDirection(input);

        Vector3 camForward = CameraTransform.forward;
        Vector3 camRight = CameraTransform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        return (camForward * input.z + camRight * input.x).normalized;
    }

    bool _hasMoveSpeedParam, _hasIsMovingParam, _hasIsSprintingParam;
    bool _checkedAnimatorParams;
    int _lastFallbackStateHash;

    void CheckAnimatorParams()
    {
        _checkedAnimatorParams = true;
        if (Animator == null) return;
        foreach (AnimatorControllerParameter param in Animator.parameters)
        {
            if (param.name == "MoveSpeed") _hasMoveSpeedParam = true;
            if (param.name == "IsMoving") _hasIsMovingParam = true;
            if (param.name == "IsSprinting") _hasIsSprintingParam = true;
        }
    }

    void UpdateAnimator(bool isMoving, bool isSprinting, float speed)
    {
        if (Animator == null)
            return;

        CalemOpenWorldDirectClipDriver direct = GetComponent<CalemOpenWorldDirectClipDriver>();
        if (direct != null && direct.IsDirectSampling)
            return;

        if (!_checkedAnimatorParams)
            CheckAnimatorParams();

        Animator.speed = 1f;

        if (_hasIsMovingParam)
            Animator.SetBool("IsMoving", isMoving);
        if (_hasIsSprintingParam)
            Animator.SetBool("IsSprinting", isSprinting);
        if (_hasMoveSpeedParam)
            Animator.SetFloat("MoveSpeed", speed);

        // Always enforce locomotion state so animation still works
        // even when controller transitions/parameters are misconfigured.
        PlayFallbackLocomotionState(isMoving, isSprinting);
    }

    void PlayFallbackLocomotionState(bool isMoving, bool isSprinting)
    {
        string stateName = isMoving ? (isSprinting ? "Run" : "Walk") : "Idle";
        int desiredHash = Animator.StringToHash(stateName);

        if (!Animator.HasState(0, desiredHash))
        {
            string legacyStateName = "player_" + stateName.ToLowerInvariant();
            desiredHash = Animator.StringToHash(legacyStateName);
            if (!Animator.HasState(0, desiredHash))
                return;
        }

        if (_lastFallbackStateHash == desiredHash)
            return;

        // Play is more reliable than CrossFade when the layer was just assigned or transitions are tight.
        Animator.Play(desiredHash, 0, 0f);
        _lastFallbackStateHash = desiredHash;
    }

    Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = ResolveKeyboardOrNull();
        if (kb != null)
        {
            float horizontal = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float vertical = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            if (Mathf.Approximately(horizontal, 0f))
                horizontal = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
            if (Mathf.Approximately(vertical, 0f))
                vertical = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);

            return new Vector2(horizontal, vertical);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        float lh = Input.GetAxisRaw("Horizontal");
        float lv = Input.GetAxisRaw("Vertical");
        if (Mathf.Approximately(lh, 0f))
            lh = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        if (Mathf.Approximately(lv, 0f))
            lv = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        return new Vector2(lh, lv);
#else
        return Vector2.zero;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// <see cref="Keyboard.current"/> is often null briefly or with certain Input System settings; fall back to any added <see cref="Keyboard"/> device.
    /// </summary>
    static Keyboard ResolveKeyboardOrNull()
    {
        if (Keyboard.current != null)
            return Keyboard.current;

        foreach (InputDevice dev in InputSystem.devices)
        {
            if (dev is Keyboard kb)
                return kb;
        }

        return null;
    }
#endif

    bool IsSprintPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = ResolveKeyboardOrNull();
        if (kb != null)
        {
            if (SprintKey == KeyCode.RightShift)
                return kb.rightShiftKey.isPressed;
            return kb.leftShiftKey.isPressed;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(SprintKey);
#else
        return false;
#endif
    }

    bool WasJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = ResolveKeyboardOrNull();
        if (kb != null && JumpKey == KeyCode.Space && kb.spaceKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(JumpKey))
            return true;
#endif
        return false;
    }

    bool IsGrounded()
    {
        if (_controller.isGrounded)
            return true;

        float half = _controller.height * 0.5f - _controller.radius;
        Vector3 center = transform.position + _controller.center;
        Vector3 foot = center + Vector3.down * half;
        float castLen = _controller.skinWidth + GroundProbeDistance;

        if (!Physics.Raycast(foot + Vector3.up * 0.06f, Vector3.down, out RaycastHit hit, castLen + 0.06f, GroundLayers, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.transform == transform || hit.transform.IsChildOf(transform))
            return false;

        return true;
    }
}
