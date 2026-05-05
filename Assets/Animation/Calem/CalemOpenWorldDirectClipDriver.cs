using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// OpenWorld fallback when Mecanim still shows a T-pose. When <see cref="directSampling"/> is true, drives clips
/// through <see cref="PlayableGraph"/> or <see cref="AnimationClip.SampleAnimation"/>.
/// Procedural leg motion is intentionally small and <b>hard-clamped</b> per bone group for generic rigs (e.g. Calem).
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class CalemOpenWorldDirectClipDriver : MonoBehaviour
{
    [Tooltip("On = drive Idle/Walk/Run clips via Playables / SampleAnimation. Off = pure Mecanim controller.")]
    public bool directSampling = true;

    [SerializeField, Tooltip("If controller yields no clips at runtime, assign the three demo .anim here.")]
    AnimationClip overrideIdle, overrideWalk, overrideRun;

    [SerializeField, Tooltip("Optional; if empty, uses CalemController from the bootstrap on this object.")]
    RuntimeAnimatorController controllerSource;

    [Header("Procedural gait (rebuilt)")]
    [Tooltip("Synthesize legs (and optional arms/pelvis) on top of idle while moving.")]
    [SerializeField] bool proceduralWalkGait = true;
    [Tooltip("While moving with procedural gait, keep sampling idle so demo walk/run clips do not fight the pose.")]
    [SerializeField] bool useIdleClipForUpperBodyWhenMoving = true;

    [SerializeField] float minStepCadenceHz = 1.55f;
    [SerializeField] float maxStepCadenceHz = 2.35f;
    [SerializeField] float runCadenceMultiplier = 1.08f;

    [Header("Limits (degrees) — drive & 3D angle from idle")]
    [Tooltip("Peak thigh swing requested from rest each step (clamped to ±Thigh hard limit).")]
    [SerializeField] float thighSwingPeakDegrees = 15f;
    [Tooltip("Hard ceiling: hinge drive and final rotation vs idle are both clamped to this magnitude.")]
    [SerializeField] float thighHardLimitDegrees = 60f;

    [SerializeField] float kneeSwingPeakDegrees = 5f;
    [SerializeField] float kneeHardLimitDegrees = 10f;
    [SerializeField] float kneePhaseLagDegrees = 22f;

    [SerializeField] bool proceduralPelvisSway = true;
    [SerializeField] float pelvisPitchPeakDegrees = 1f;
    [SerializeField] float pelvisRollPeakDegrees = 1f;
    // BUG 1 FIX: was `= f;` (missing numeric literal — compile error). Corrected to 2f.
    [SerializeField] float pelvisHardLimitDegrees = 2f;
    [SerializeField] Vector3 pelvisPitchAxisLocal = new Vector3(1f, 0f, 0f);
    [SerializeField] Vector3 pelvisRollAxisLocal = new Vector3(0f, 1f, 0f);

    [SerializeField] bool proceduralArmSwing = true;
    [SerializeField] float armSwingPeakDegrees = 6f;
    [SerializeField] float armHardLimitDegrees = 10f;
    [SerializeField] float armPhaseOffsetDegrees = 90f;
    [SerializeField] float elbowBendPeakDegrees = 4f;
    [SerializeField] float elbowHardLimitDegrees = 10f;

    [Tooltip("Flip if legs step backward or knees bend the wrong way.")]
    [SerializeField] float legSwingDirectionSign = 1f;

    public enum LegSwingPlaneMode
    {
        Sagittal,
        Coronal
    }

    [Tooltip("Sagittal = forward/back (normal walk). Coronal = swing around character forward so motion reads more left/right.")]
    [SerializeField] LegSwingPlaneMode legSwingPlane = LegSwingPlaneMode.Sagittal;

    [Tooltip("Mirrored rigs: same signed hinge on LThigh/RThigh often moves both legs the same way in world space. On = negate right thigh/shin drives so left and right alternate.")]
    [SerializeField] bool invertRightLegSwing = false;

    [SerializeField, Tooltip("Log CalemOW-DRIVER-STATUS once after build.")]
    bool logDiagnostics = true;
    bool _driverStatusLogged;

    public bool IsDirectSampling { get; private set; }

    public enum LocomotionMode
    {
        Inactive = 0,
        PlayableGraph = 1,
        SampleAnimation = 2
    }

    public LocomotionMode CurrentMode { get; private set; } = LocomotionMode.Inactive;

    OpenWorldPlayerController _player;
    CalemOpenWorldAnimatorBootstrap _bootstrap;
    Animator _animator;
    GameObject _sampleTarget;
    AnimationClip _idle, _walk, _run;
    AnimationClip _active;
    // BUG 5 FIX: removed unused _time field. _poseSampleTime already handles SampleAnimation timing.
    float _poseSampleTime;
    AnimationClip _lastSampledClip;
    bool _aborted;
    bool _loggedMissingRig;

    // BUG 4 FIX: cached rest rotations captured once at clip assignment (t=0), so the procedural
    // gait always adds on top of a stable baseline rather than a frame-advancing idle pose.
    Quaternion _restLTh, _restRTh, _restLLg, _restRLg;
    Quaternion _restLArm, _restRArm, _restLFore, _restRFore;
    Quaternion _restPelvis;
    bool _restCached;

    PlayableGraph _graph;
    AnimationPlayableOutput _out;
    AnimationClipPlayable _clipPl;

    void Awake()
    {
        _player = GetComponent<OpenWorldPlayerController>();
        _bootstrap = GetComponent<CalemOpenWorldAnimatorBootstrap>();
    }

    void OnEnable()
    {
        if (Application.isPlaying)
            TryBuild();
    }

    public void RebuildFromController()
    {
        _aborted = false;
        _loggedMissingRig = false;
        _driverStatusLogged = false;
        Shutdown();
        IsDirectSampling = false;
        CurrentMode = LocomotionMode.Inactive;
        _idle = _walk = _run = _active = null;
        _poseSampleTime = 0f;
        _lastSampledClip = null;
        _restCached = false;
        TryBuild();
    }

    public void DisposePlayableGraphOnly()
    {
        Shutdown();
        IsDirectSampling = false;
        CurrentMode = LocomotionMode.Inactive;
    }

    void OnDestroy()
    {
        Shutdown();
    }

    void OnDisable()
    {
        Shutdown();
    }

    void Shutdown()
    {
        if (_graph.IsValid())
        {
            _graph.Stop();
            _graph.Destroy();
        }
    }

    void TryBuild()
    {
        if (IsDirectSampling)
            return;
        if (_aborted)
            return;
        if (!directSampling)
        {
            if (logDiagnostics)
                LogDriverStatus("OFF", "directSampling is unchecked — locomotion uses Mecanim only (Animator Controller + Avatar).");
            return;
        }

        if (_player == null)
            _player = GetComponent<OpenWorldPlayerController>();
        if (_bootstrap == null)
            _bootstrap = GetComponent<CalemOpenWorldAnimatorBootstrap>();

        _animator = OpenWorldRigAnimators.FindRigAnimator(transform);
        if (_animator == null)
        {
            if (logDiagnostics && !_loggedMissingRig)
            {
                _loggedMissingRig = true;
                Debug.Log(
                    "[CalemOW-DRIVER-STATUS] WAIT | " + name +
                    " | Rig Animator not found yet (retrying each frame until PlayerAvatarVisual exists).",
                    this);
            }
            return;
        }

        ResolveClips();
        if (_idle == null)
        {
            LogOnce("[CalemOW] No idle clip. Assign CalemController on bootstrap and/or " +
                    "Override Idle/Walk/Run on CalemOpenWorldDirectClipDriver.", true);
            _aborted = true;
            if (logDiagnostics)
                LogDriverStatus("FAIL", "No idle clip. Assign CalemController on the bootstrap and/or override clips on this driver.");
            return;
        }

        if (!BuildSampleTarget())
        {
            LogOnce("[CalemOW] Could not resolve sample root under " + _animator.name, true);
            _aborted = true;
            if (logDiagnostics)
                LogDriverStatus("FAIL", "BuildSampleTarget failed for " + _animator.name);
            return;
        }

        if (TryBeginSampleLocomotion())
        {
            CurrentMode = LocomotionMode.SampleAnimation;
            IsDirectSampling = true;
        }
        else if (TryBuildPlayable())
        {
            CurrentMode = LocomotionMode.PlayableGraph;
            IsDirectSampling = true;
            if (_animator != null)
            {
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _animator.enabled = true;
            }
        }
        else
        {
            IsDirectSampling = false;
            CurrentMode = LocomotionMode.Inactive;
            _aborted = true;
        }

        if (logDiagnostics && !_driverStatusLogged)
        {
            LogDriverDetailLine(IsDirectSampling
                ? null
                : "SampleAnimation and PlayableGraph both failed — Mode=Inactive; check clip import and rig.");
        }
    }

    bool TryBeginSampleLocomotion()
    {
        if (_animator == null || _sampleTarget == null || _idle == null)
            return false;

        try
        {
            _active = _idle;
            _poseSampleTime = 0f;
            _lastSampledClip = null;
            _restCached = false;
            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
            _animator.runtimeAnimatorController = null;
            _animator.enabled = false;
            _idle.SampleAnimation(_sampleTarget, 0f);
            // BUG 4 FIX: cache rest rotations immediately after the t=0 sample, before any
            // procedural code runs. This gives a stable, non-drifting baseline every frame.
            CacheRestRotations();
            return true;
        }
        catch (System.Exception e)
        {
            LogOnce("[CalemOW] SampleAnimation init failed: " + e.Message, true);
            return false;
        }
    }

    /// <summary>
    /// BUG 4 FIX: Snapshots the rest (idle t=0) local rotations for all procedural bones.
    /// Called once after the idle clip is sampled at t=0. Subsequent frames use these cached
    /// values as the baseline so the procedural gait does not drift with the advancing idle pose.
    /// </summary>
    void CacheRestRotations()
    {
        if (_sampleTarget == null)
            return;

        Transform root = _sampleTarget.transform;

        Transform lTh   = FindBoneByName(root, "LThigh");
        Transform rTh   = FindBoneByName(root, "RThigh");
        Transform lLg   = FindBoneByName(root, "LLeg");
        Transform rLg   = FindBoneByName(root, "RLeg");
        Transform lArm  = FindBoneByName(root, "LArmA") ?? FindBoneByName(root, "LArm");
        Transform rArm  = FindBoneByName(root, "RArmA") ?? FindBoneByName(root, "RArm");
        Transform lFore = FindBoneByName(root, "LArmB") ?? FindBoneByName(root, "LForeArm");
        Transform rFore = FindBoneByName(root, "RArmB") ?? FindBoneByName(root, "RForeArm");
        Transform pelvis = FindPelvisBone(root);

        _restLTh    = lTh   != null ? lTh.localRotation   : Quaternion.identity;
        _restRTh    = rTh   != null ? rTh.localRotation   : Quaternion.identity;
        _restLLg    = lLg   != null ? lLg.localRotation   : Quaternion.identity;
        _restRLg    = rLg   != null ? rLg.localRotation   : Quaternion.identity;
        _restLArm   = lArm  != null ? lArm.localRotation  : Quaternion.identity;
        _restRArm   = rArm  != null ? rArm.localRotation  : Quaternion.identity;
        _restLFore  = lFore != null ? lFore.localRotation : Quaternion.identity;
        _restRFore  = rFore != null ? rFore.localRotation : Quaternion.identity;
        _restPelvis = pelvis != null ? pelvis.localRotation : Quaternion.identity;

        _restCached = true;
    }

    void ResolveClips()
    {
        if (overrideIdle != null)
        {
            _idle = overrideIdle;
            _walk = overrideWalk != null ? overrideWalk : _idle;
            _run  = overrideRun  != null ? overrideRun  : _walk;
            return;
        }

        RuntimeAnimatorController c = controllerSource != null
            ? controllerSource
            : (_bootstrap != null ? _bootstrap.CalemController : null);
        if (c == null)
            return;

        var clips = c.animationClips;
        if (clips == null || clips.Length == 0)
        {
            LogOnce("[CalemOW] RuntimeAnimatorController.animationClips is empty. Assign " +
                    "Override clips on the driver or re-link the CalemController.", false);
            return;
        }

        foreach (AnimationClip ac in clips)
        {
            if (ac == null) continue;
            string n = ac.name;
            if (_idle == null && (n == "player_idle" || n.Contains("idle", System.StringComparison.OrdinalIgnoreCase)))
                _idle = ac;
            if (_walk == null && (n == "player_walk" || n.Contains("walk", System.StringComparison.OrdinalIgnoreCase)))
                _walk = ac;
            if (_run  == null && (n == "player_run"  || n.Contains("run",  System.StringComparison.OrdinalIgnoreCase)))
                _run  = ac;
        }

        if (_idle == null) _idle = clips[0];
        if (_walk == null) _walk = _idle;
        if (_run  == null) _run  = _walk;

        if (_walk != null && _walk.empty)
        {
            LogOnce("[CalemOW] Walk clip has no animation curves; using idle clip for walk.", false);
            _walk = _idle;
        }
        if (_run != null && _run.empty)
        {
            LogOnce("[CalemOW] Run clip has no animation curves; using walk clip for run.", false);
            _run = _walk;
        }

        _active = _idle;
    }

    bool BuildSampleTarget()
    {
        _sampleTarget = _animator.gameObject;
        if (_animator.transform.Find("tr0002_00_ba") != null)
            return true;

        foreach (Transform t in _animator.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (t.name == "tr0002_00_ba")
            {
                _sampleTarget = t.parent != null ? t.parent.gameObject : _animator.gameObject;
                return true;
            }
        }

        return true;
    }

    bool TryBuildPlayable()
    {
        if (_animator == null || _idle == null)
            return false;

        try
        {
            Shutdown();
            _graph = PlayableGraph.Create("CalemOpenWorldLocom");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            _clipPl = AnimationClipPlayable.Create(_graph, _idle);
            _clipPl.SetSpeed(1f);
            _clipPl.SetDuration(_idle.length > 0.001f ? _idle.length : 1f);
            _clipPl.SetTime(0d);

            _out = AnimationPlayableOutput.Create(_graph, "Calem", _animator);
            _out.SetSourcePlayable(_clipPl);

            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.updateMode = AnimatorUpdateMode.Normal;
            _animator.runtimeAnimatorController = null;
            _animator.Rebind();
            _animator.enabled = true;
            _graph.Play();

            // BUG 4 FIX: evaluate once at t=0 so the Animator has a valid pose before
            // CacheRestRotations reads bone transforms.
            _graph.Evaluate(0f);
            CacheRestRotations();
        }
        catch (System.Exception e)
        {
            LogOnce("[CalemOW] PlayableGraph failed: " + e.Message, true);
            Shutdown();
            if (_animator != null)
                _animator.enabled = false;
            return false;
        }

        return _graph.IsValid() && _clipPl.IsValid();
    }

    void SwitchActiveClip(AnimationClip clip)
    {
        if (clip == null || clip == _active)
            return;
        _active = clip;
        // BUG 4 FIX: invalidate cached rests so they are re-snapshotted at t=0 of the new clip.
        _restCached = false;
        if (CurrentMode != LocomotionMode.PlayableGraph)
            return;
        if (TryRebuildPlayableForClip(clip))
            return;
        if (EnterSampleModeAfterPlayableFailed())
        {
            CurrentMode = LocomotionMode.SampleAnimation;
            if (_animator != null)
                _animator.enabled = false;
        }
        else
        {
            CurrentMode = LocomotionMode.Inactive;
        }
    }

    bool EnterSampleModeAfterPlayableFailed()
    {
        if (!BuildSampleTarget() || _active == null)
            return false;
        if (_animator != null)
        {
            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
            _animator.runtimeAnimatorController = null;
            _animator.enabled = false;
        }
        _active.SampleAnimation(_sampleTarget, 0f);
        _restCached = false;
        CacheRestRotations();
        return true;
    }

    bool TryRebuildPlayableForClip(AnimationClip clip)
    {
        if (_animator == null || clip == null)
            return false;
        try
        {
            Shutdown();
            _graph = PlayableGraph.Create("CalemOpenWorldLocom");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _clipPl = AnimationClipPlayable.Create(_graph, clip);
            _clipPl.SetSpeed(1f);
            _clipPl.SetDuration(clip.length > 0.001f ? clip.length : 1f);
            _clipPl.SetTime(0d);
            _out = AnimationPlayableOutput.Create(_graph, "Calem", _animator);
            _out.SetSourcePlayable(_clipPl);
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.updateMode = AnimatorUpdateMode.Normal;
            _animator.runtimeAnimatorController = null;
            _animator.Rebind();
            _animator.enabled = true;
            _graph.Play();
            _graph.Evaluate(0f);
            // BUG 4 FIX: re-cache rests for the new clip.
            _restCached = false;
            CacheRestRotations();
        }
        catch (System.Exception e)
        {
            LogOnce("[CalemOW] Rebuild Playable: " + e.Message, true);
            Shutdown();
            return false;
        }
        return _graph.IsValid() && _clipPl.IsValid();
    }

    void LateUpdate()
    {
        if (directSampling && !IsDirectSampling)
            TryBuild();
        if (!directSampling || !IsDirectSampling)
            return;

        ResolvePlayerControllerIfNeeded();

        AnimationClip desired = ResolveDesiredLocomotionClip();
        if (desired == null)
            return;

        if (_active != desired)
            SwitchActiveClip(desired);

        if (_active == null)
            return;

        bool wantsMove = ComputeWantsLocomotion(out bool wantsRun, out float horizontalSpeed);

        if (CurrentMode == LocomotionMode.PlayableGraph)
        {
            if (_graph.IsValid())
                _graph.Evaluate(Time.deltaTime);
            // BUG 4 FIX: re-cache rests after the first ever graph evaluation if not yet done.
            if (!_restCached)
                CacheRestRotations();
            if (proceduralWalkGait && wantsMove)
                ApplyProceduralWalkGait(isRun: wantsRun, horizontalSpeed);
            return;
        }

        if (CurrentMode != LocomotionMode.SampleAnimation)
            return;

        if (!ReferenceEquals(_lastSampledClip, _active))
        {
            _lastSampledClip = _active;
            _poseSampleTime = 0f;
            // BUG 4 FIX: re-cache rests when clip changes.
            _restCached = false;
        }

        float len = _active.length > 0.001f ? _active.length : 1f;
        _poseSampleTime = (_poseSampleTime + Time.deltaTime) % len;
        _active.SampleAnimation(_sampleTarget, _poseSampleTime);

        if (!_restCached)
            CacheRestRotations();

        if (proceduralWalkGait && wantsMove)
            ApplyProceduralWalkGait(isRun: wantsRun, horizontalSpeed);
    }

    bool ComputeWantsLocomotion(out bool wantsRun, out float horizontalSpeed)
    {
        wantsRun = false;
        horizontalSpeed = 0f;
        if (_player == null)
            return false;

        bool moving = _player.LocomotionIsMoving;
        if (!moving && TryGetHorizontalVelocitySq(_player, out float vSq))
            moving = vSq > 0.04f;

        TryGetHorizontalVelocitySq(_player, out float v2);
        horizontalSpeed = Mathf.Sqrt(v2);

        wantsRun = _player.LocomotionIsSprinting;
        if (moving && !wantsRun && TryGetHorizontalVelocitySq(_player, out float vFast))
            wantsRun = vFast > _player.SprintSpeed * _player.SprintSpeed * 0.64f;

        return moving;
    }

    /// <summary>
    /// Minimal smooth walk: smoothstep envelopes from sin phase, one shared sagittal hinge, strict ±limits per region.
    ///
    /// BUG 2 FIX: Restructured into two passes — all drives are computed first (read-only),
    /// then all bones are written in a second pass. This ensures that when the knee's
    /// ApplySwingFromWorldHingeWithParent call uses parentWorldRot, it uses the thigh's
    /// pre-modification world rotation, so the sagittal axis is always correct.
    ///
    /// BUG 3 FIX: Arm swing now uses separate positive/negative sin envelopes for left and right
    /// so the arm swings fully in both directions rather than only one.
    ///
    /// BUG 4 FIX: Uses _rest* cached fields instead of reading localRotation live, so the
    /// baseline does not drift as the idle clip advances frame-to-frame.
    /// </summary>
    void ApplyProceduralWalkGait(bool isRun, float horizontalSpeed)
    {
        if (_sampleTarget == null || _player == null || !_restCached)
            return;

        Transform root = _sampleTarget.transform;
        Transform lTh  = FindBoneByName(root, "LThigh");
        Transform rTh  = FindBoneByName(root, "RThigh");
        Transform lLg  = FindBoneByName(root, "LLeg");
        Transform rLg  = FindBoneByName(root, "RLeg");
        if (lTh == null || rTh == null)
            return;

        float thighCap  = Mathf.Max(0.5f, thighHardLimitDegrees);
        float kneeCap   = Mathf.Max(0.5f, kneeHardLimitDegrees);
        float pelvisCap = Mathf.Max(0.5f, pelvisHardLimitDegrees);
        float armCap    = Mathf.Max(0.5f, armHardLimitDegrees);
        float elbowCap  = Mathf.Max(0.5f, elbowHardLimitDegrees);

        float refSpeed  = isRun ? Mathf.Max(0.01f, _player.SprintSpeed) : Mathf.Max(0.01f, _player.MoveSpeed);
        float speedNorm = Mathf.Clamp01(horizontalSpeed / refSpeed);
        float cadence   = Mathf.Lerp(minStepCadenceHz, maxStepCadenceHz, speedNorm);
        if (isRun) cadence *= runCadenceMultiplier;

        float phase  = Mathf.Repeat(Time.time * cadence, 1f) * Mathf.PI * 2f;
        float runMul = isRun ? 1.12f : 1f;

        float dir = Mathf.Abs(legSwingDirectionSign) < 1e-4f ? 1f : Mathf.Sign(legSwingDirectionSign);

        // ── Leg drives ──────────────────────────────────────────────────────────────────────
        float leftEnv  = SmoothStep01(Mathf.Max(0f,  Mathf.Sin(phase)));
        float rightEnv = SmoothStep01(Mathf.Max(0f, -Mathf.Sin(phase)));

        float thighPeak   = Mathf.Min(thighSwingPeakDegrees * runMul, thighCap);
        float supportPull = thighPeak * 0.22f;
        float thighL = Mathf.Clamp((leftEnv  * thighPeak - rightEnv * supportPull) * dir, -thighCap, thighCap);
        float thighR = Mathf.Clamp((rightEnv * thighPeak - leftEnv  * supportPull) * dir, -thighCap, thighCap);

        float kneePhase = phase - kneePhaseLagDegrees * Mathf.Deg2Rad;
        float kLeftEnv  = SmoothStep01(Mathf.Max(0f,  Mathf.Sin(kneePhase)));
        float kRightEnv = SmoothStep01(Mathf.Max(0f, -Mathf.Sin(kneePhase)));
        float kneePeak = Mathf.Min(kneeSwingPeakDegrees * runMul, kneeCap);
        float kneeL = Mathf.Clamp(kLeftEnv  * kneePeak, 0f, kneeCap);
        float kneeR = Mathf.Clamp(kRightEnv * kneePeak, 0f, kneeCap);

        // ── Arm drives ──────────────────────────────────────────────────────────────────────
        // BUG 3 FIX: was SmoothStep01(Mathf.Sin(armPhase)) — the negative sin half was
        // clamped to zero, making arms swing one-directional only.
        float armPhaseRad = phase + armPhaseOffsetDegrees * Mathf.Deg2Rad;
        float armEnvL  = SmoothStep01(Mathf.Max(0f, -Mathf.Sin(armPhaseRad)));
        float armEnvR  = SmoothStep01(Mathf.Max(0f,  Mathf.Sin(armPhaseRad)));
        float armPeak  = Mathf.Min(armSwingPeakDegrees * runMul, armCap);
        float armL = Mathf.Clamp(armEnvL * armPeak * dir, -armCap, armCap);
        float armR = Mathf.Clamp(armEnvR * armPeak * dir, -armCap, armCap);

        float elbowWave = Mathf.Clamp01(Mathf.Sin(armPhaseRad));
        float elPeak = Mathf.Min(elbowBendPeakDegrees * runMul, elbowCap);
        float elL = Mathf.Clamp(elbowWave        * elPeak, 0f, elbowCap);
        float elR = Mathf.Clamp((1f - elbowWave) * elPeak, 0f, elbowCap);

        Vector3 legHinge = legSwingPlane == LegSwingPlaneMode.Coronal
            ? ComputeCoronalHingeAxis(root)
            : ComputeSagittalHingeAxis(root);
        Vector3 armHinge = ComputeSagittalHingeAxis(root);

        float rLegMul = invertRightLegSwing ? -1f : 1f;

        // ── BUG 2 FIX: two-pass write ────────────────────────────────────────────────────────
        // PASS 1 — snapshot world rotations of all PARENTS before any bone is written.
        // Without this, the knee's parent (thigh) is already modified when its axis is
        // computed, giving a wrong axis that compounds every frame into 360° rotations.
        Quaternion lThParentWorld  = lTh.parent  != null ? lTh.parent.rotation  : Quaternion.identity;
        Quaternion rThParentWorld  = rTh.parent  != null ? rTh.parent.rotation  : Quaternion.identity;
        Quaternion lLgParentWorld  = lLg != null && lLg.parent != null ? lLg.parent.rotation : Quaternion.identity;
        Quaternion rLgParentWorld  = rLg != null && rLg.parent != null ? rLg.parent.rotation : Quaternion.identity;

        Transform lArm  = proceduralArmSwing ? (FindBoneByName(root, "LArmA") ?? FindBoneByName(root, "LArm"))      : null;
        Transform rArm  = proceduralArmSwing ? (FindBoneByName(root, "RArmA") ?? FindBoneByName(root, "RArm"))      : null;
        Transform lFore = proceduralArmSwing ? (FindBoneByName(root, "LArmB") ?? FindBoneByName(root, "LForeArm")) : null;
        Transform rFore = proceduralArmSwing ? (FindBoneByName(root, "RArmB") ?? FindBoneByName(root, "RForeArm")) : null;

        Quaternion lArmParentWorld  = lArm  != null && lArm.parent  != null ? lArm.parent.rotation  : Quaternion.identity;
        Quaternion rArmParentWorld  = rArm  != null && rArm.parent  != null ? rArm.parent.rotation  : Quaternion.identity;
        Quaternion lForeParentWorld = lFore != null && lFore.parent != null ? lFore.parent.rotation : Quaternion.identity;
        Quaternion rForeParentWorld = rFore != null && rFore.parent != null ? rFore.parent.rotation : Quaternion.identity;

        // PASS 2 — write all bones using pre-snapshotted parent worlds and stable cached rests.
        ApplySwingFromWorldHingeWithParent(lTh,  _restLTh,   lThParentWorld,  legHinge,  thighL,       thighCap);
        ApplySwingFromWorldHingeWithParent(rTh,  _restRTh,   rThParentWorld,  legHinge,  thighR * rLegMul,       thighCap);
        if (lLg != null)
            ApplySwingFromWorldHingeWithParent(lLg, _restLLg, lLgParentWorld, legHinge, -kneeL * dir,  kneeCap);
        if (rLg != null)
            ApplySwingFromWorldHingeWithParent(rLg, _restRLg, rLgParentWorld, legHinge, -kneeR * dir * rLegMul,  kneeCap);

        if (proceduralArmSwing && lArm != null && rArm != null)
        {
            ApplySwingFromWorldHingeWithParent(lArm, _restLArm, lArmParentWorld, armHinge,  armL,       armCap);
            ApplySwingFromWorldHingeWithParent(rArm, _restRArm, rArmParentWorld, armHinge, -armR,       armCap);
            if (lFore != null)
                ApplySwingFromWorldHingeWithParent(lFore, _restLFore, lForeParentWorld, armHinge, -elL * dir, elbowCap);
            if (rFore != null)
                ApplySwingFromWorldHingeWithParent(rFore, _restRFore, rForeParentWorld, armHinge, -elR * dir, elbowCap);
        }

        if (proceduralPelvisSway)
            ApplyPelvisSway(root, phase, speedNorm, pelvisCap);
    }

    void ApplyPelvisSway(Transform root, float phase, float speedNorm, float hardLimitDeg)
    {
        if (root == null) return;

        Transform pelvis = FindPelvisBone(root);
        if (pelvis == null) return;

        // BUG 4 FIX: use cached rest instead of live localRotation.
        Quaternion rest = _restCached ? _restPelvis : pelvis.localRotation;
        float blend = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(speedNorm));
        float pitch = Mathf.Sin(phase * 2f) * pelvisPitchPeakDegrees * blend;
        float roll  = Mathf.Sin(phase)      * pelvisRollPeakDegrees  * blend;

        pitch = Mathf.Clamp(pitch, -hardLimitDeg, hardLimitDeg);
        roll  = Mathf.Clamp(roll,  -hardLimitDeg, hardLimitDeg);

        Vector3 pitchAx = pelvisPitchAxisLocal.sqrMagnitude > 1e-8f ? pelvisPitchAxisLocal.normalized : Vector3.right;
        Vector3 rollAx  = pelvisRollAxisLocal.sqrMagnitude  > 1e-8f ? pelvisRollAxisLocal.normalized  : Vector3.up;

        Quaternion q = rest;
        if (Mathf.Abs(pitch) > 1e-5f) q *= Quaternion.AngleAxis(pitch, pitchAx);
        if (Mathf.Abs(roll)  > 1e-5f) q *= Quaternion.AngleAxis(roll,  rollAx);
        pelvis.localRotation = q;
        ClampLocalRotationNearRest(pelvis, rest, hardLimitDeg);
    }

    /// <summary>
    /// BUG 2 FIX: Replaces ApplySwingFromWorldHinge. Accepts an explicit parentWorldRot snapshot
    /// taken before any bone in this frame was written, guaranteeing the axis is always computed
    /// from the rest/idle orientation regardless of sibling write order.
    /// </summary>
    static void ApplySwingFromWorldHingeWithParent(
        Transform bone,
        Quaternion restLocal,
        Quaternion parentWorldRot,
        Vector3 worldHingeUnit,
        float degrees,
        float hardLimitDeg)
    {
        if (bone == null) return;

        float d = Mathf.Clamp(degrees, -hardLimitDeg, hardLimitDeg);

        if (Mathf.Abs(d) < 1e-5f)
        {
            bone.localRotation = restLocal;
            return;
        }

        if (worldHingeUnit.sqrMagnitude < 1e-8f)
            return;

        worldHingeUnit.Normalize();

        if (bone.parent == null)
        {
            bone.localRotation = Quaternion.AngleAxis(d, worldHingeUnit) * restLocal;
            ClampLocalRotationNearRest(bone, restLocal, hardLimitDeg);
            return;
        }

        // Use the PRE-WRITE parent world rotation (snapshot from before any bones were modified).
        Quaternion worldRotRest = parentWorldRot * restLocal;
        Vector3 axisLocal = (Quaternion.Inverse(worldRotRest) * worldHingeUnit).normalized;
        if (axisLocal.sqrMagnitude < 1e-8f)
            axisLocal = Vector3.right;

        bone.localRotation = restLocal * Quaternion.AngleAxis(d, axisLocal);
        ClampLocalRotationNearRest(bone, restLocal, hardLimitDeg);
    }

    static void ClampLocalRotationNearRest(Transform bone, Quaternion restLocal, float maxDegreesFromRest)
    {
        if (bone == null || maxDegreesFromRest <= 0.01f)
            return;

        float ang = Quaternion.Angle(restLocal, bone.localRotation);
        if (ang <= maxDegreesFromRest + 1e-3f)
            return;

        bone.localRotation = Quaternion.Slerp(restLocal, bone.localRotation, maxDegreesFromRest / Mathf.Max(ang, 1e-4f));
        ang = Quaternion.Angle(restLocal, bone.localRotation);
        if (ang > maxDegreesFromRest + 0.05f)
            bone.localRotation = Quaternion.Slerp(restLocal, bone.localRotation, maxDegreesFromRest / Mathf.Max(ang, 1e-4f));
        if (Quaternion.Angle(restLocal, bone.localRotation) > maxDegreesFromRest + 0.25f)
            bone.localRotation = restLocal;
    }

    static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static Vector3 ComputeSagittalHingeAxis(Transform characterRoot)
    {
        Vector3 fwd = characterRoot.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-8f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 hinge = Vector3.Cross(Vector3.up, fwd);
        if (hinge.sqrMagnitude < 1e-8f) hinge = characterRoot.right;
        return hinge.normalized;
    }

    /// <summary>Axis along flattened forward — flex around this tends to read as legs moving left/right (coronal) vs sagittal.</summary>
    static Vector3 ComputeCoronalHingeAxis(Transform characterRoot)
    {
        Vector3 fwd = characterRoot.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-8f)
            fwd = Vector3.forward;
        return fwd.normalized;
    }

    static Transform FindBoneByName(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && string.Equals(t.name, boneName, System.StringComparison.Ordinal))
                return t;
        }
        return null;
    }

    static Transform FindPelvisBone(Transform root)
    {
        Transform lThigh = FindBoneByName(root, "LThigh");
        if (lThigh != null && lThigh.parent != null)
            return lThigh.parent;

        string[] names =
        {
            "Waist", "waist", "Hips", "hips", "HIP", "Pelvis", "pelvis",
            "Root", "root", "Bip01 Pelvis", "Bip001 Pelvis",
            "mixamorig:Hips", "mixamorig_Hips",
        };
        foreach (string n in names)
        {
            Transform t = FindBoneByName(root, n);
            if (t != null) return t;
        }
        return null;
    }

    void ResolvePlayerControllerIfNeeded()
    {
        if (_player != null && _player.isActiveAndEnabled) return;
        _player = GetComponent<OpenWorldPlayerController>();
        if (_player == null)
            _player = GetComponentInParent<OpenWorldPlayerController>();
    }

    AnimationClip ResolveDesiredLocomotionClip()
    {
        AnimationClip desired = _idle;
        bool moving = false;
        bool sprint = false;

        if (_player != null)
        {
            moving = _player.LocomotionIsMoving;
            sprint = _player.LocomotionIsSprinting;

            if (!moving && TryGetHorizontalVelocitySq(_player, out float vSq))
                moving = vSq > 0.04f;

            if (moving && !sprint && TryGetHorizontalVelocitySq(_player, out float vSq2))
                sprint = vSq2 > _player.SprintSpeed * _player.SprintSpeed * 0.64f;
        }

        if (!moving) return desired;

        if (proceduralWalkGait && useIdleClipForUpperBodyWhenMoving && _idle != null)
            return _idle;

        if (sprint) return _run != null ? _run : _walk;
        return _walk != null ? _walk : _idle;
    }

    static bool TryGetHorizontalVelocitySq(OpenWorldPlayerController player, out float horizontalVelocitySq)
    {
        horizontalVelocitySq = 0f;
        if (player == null) return false;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc == null) return false;
        Vector3 v = cc.velocity;
        v.y = 0f;
        horizontalVelocitySq = v.sqrMagnitude;
        return true;
    }

    void LogOnce(string message, bool error)
    {
        if (error) Debug.LogError(message, this);
        else       Debug.LogWarning(message, this);
    }

    void LogDriverStatus(string result, string detail)
    {
        if (!logDiagnostics || _driverStatusLogged) return;
        _driverStatusLogged = true;
        Debug.Log("[CalemOW-DRIVER-STATUS] " + result + " | " + name + " | " + detail, this);
    }

    void LogDriverDetailLine(string hint = null)
    {
        if (!logDiagnostics || _driverStatusLogged) return;
        _driverStatusLogged = true;
        int clipCount = _bootstrap != null && _bootstrap.CalemController != null
            ? (_bootstrap.CalemController.animationClips != null
                ? _bootstrap.CalemController.animationClips.Length : 0)
            : -1;
        var names = new List<string>(8);
        if (_bootstrap != null && _bootstrap.CalemController != null && _bootstrap.CalemController.animationClips != null)
            foreach (AnimationClip c in _bootstrap.CalemController.animationClips)
                if (c != null) names.Add(c.name);

        string result = IsDirectSampling ? "OK" : "INACTIVE";
        string extra  = string.IsNullOrEmpty(hint) ? "" : " | " + hint;
        Debug.Log(
            "[CalemOW-DRIVER-STATUS] " + result + " | " + name +
            " | Mode=" + CurrentMode +
            ", directSampling=" + directSampling +
            ", IsDirectSampling=" + IsDirectSampling +
            ", idleClip=" + (_idle != null ? _idle.name : "null") +
            ", Animator=" + (_animator != null ? _animator.name : "null") +
            ", isHuman=" + (_animator != null && _animator.isHuman) +
            ", SampleTarget=" + (_sampleTarget != null ? _sampleTarget.name : "null") +
            ", controllerClips=" + clipCount + " [" + string.Join(", ", names) + "]" +
            extra,
            this);
    }
}
