using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class CalemOpenWorldAnimatorBootstrap : MonoBehaviour
{
    [Tooltip("Animator on the OpenWorld player. If empty, one will be searched automatically.")]
    public Animator TargetAnimator;

    [Tooltip("Controller assigned to Calem in OpenWorld.")]
    public RuntimeAnimatorController CalemController;

    [Tooltip("Removes broken/empty AnimationEvents from runtime clips to avoid console spam. Disable for humanoid clips.")]
    public bool RemoveInvalidAnimationEvents = false;

    [Tooltip("If no Animator is found in the same frame as startup (nested prefab timing), keep searching for this many frames before logging failure.")]
    [SerializeField] int _rigSearchMaxFrames = 120;

    bool _deferredRigSearchRunning;
    Coroutine _deferredRigCoroutine;

    void OnDestroy()
    {
        StopDeferredRigSearch();
    }

    /// <summary>Call after a runtime rig is parented under the player (e.g. <c>OpenWorldEncounterManager</c> avatar spawn).</summary>
    public void RefreshRigFromHierarchy()
    {
        if (!isActiveAndEnabled)
            return;
        StopDeferredRigSearch();
        TargetAnimator = null;
        ApplyControllerIfReady();
    }

    void StopDeferredRigSearch()
    {
        if (_deferredRigCoroutine != null)
        {
            StopCoroutine(_deferredRigCoroutine);
            _deferredRigCoroutine = null;
        }
        _deferredRigSearchRunning = false;
    }

    void Awake()
    {
        ApplyControllerIfReady();
    }

    void Start()
    {
        ApplyControllerIfReady();
    }

    public void ApplyControllerIfReady()
    {
        if (TargetAnimator != null && !TargetAnimator.transform.IsChildOf(transform))
            TargetAnimator = null;

        // Always prefer the runtime-spawned visual so a stale serialized TargetAnimator never wins.
        Transform vis = transform.Find("PlayerAvatarVisual");
        if (vis != null)
        {
            Animator onVis = vis.GetComponent<Animator>() ?? vis.GetComponentInChildren<Animator>(true);
            if (onVis != null)
                TargetAnimator = onVis;
        }

        if (TargetAnimator == null)
            TargetAnimator = OpenWorldRigAnimators.FindRigAnimator(transform, false);
        if (TargetAnimator == null)
        {
            // Nested prefab / load order: hierarchy can be empty in parent Awake; try again for several frames
            if (isActiveAndEnabled)
                StartDeferredRigSearchIfNeeded();
            return;
        }

        if (CalemController == null)
        {
            LogBootstrapOncePerSession(
                "SKIP",
                "CalemController is not assigned. Drag CalemOpenWorld.controller (or your locomotion controller) into the bootstrap field.");
            return;
        }

        RuntimeAnimatorController controllerToUse = CalemController;
        if (RemoveInvalidAnimationEvents)
            controllerToUse = BuildRuntimeControllerWithoutInvalidEvents(CalemController);

        TargetAnimator.runtimeAnimatorController = controllerToUse;
        TargetAnimator.enabled = true;
        TargetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        TargetAnimator.Rebind();
        TargetAnimator.Update(0f);
        int idleHash = Animator.StringToHash("Idle");
        if (TargetAnimator.HasState(0, idleHash))
            TargetAnimator.Play(idleHash, 0, 0f);
        else if (TargetAnimator.HasState(0, Animator.StringToHash("player_idle")))
            TargetAnimator.Play(Animator.StringToHash("player_idle"), 0, 0f);

        OpenWorldPlayerController playerController = GetComponent<OpenWorldPlayerController>();
        if (playerController != null)
        {
            playerController.Animator = TargetAnimator;
            playerController.OnRigAnimatorBound();
        }

        CalemOpenWorldDirectClipDriver direct = GetComponent<CalemOpenWorldDirectClipDriver>();
        if (direct == null)
            direct = gameObject.AddComponent<CalemOpenWorldDirectClipDriver>();
        // Locomotion clips are generic Mecanim animations on this rig; Playable/sample path is always enabled for open world.
        direct.directSampling = true;
        direct.RebuildFromController();

        // Any active direct driver mode (Playable or per-frame Sample) owns the rig; do not restore Mecanim controller.
        bool driverOwnsRig = direct.IsDirectSampling;
        if (!driverOwnsRig && TargetAnimator.runtimeAnimatorController == null && controllerToUse != null)
        {
            TargetAnimator.runtimeAnimatorController = controllerToUse;
            TargetAnimator.Rebind();
            TargetAnimator.Update(0f);
        }

        // PlayableGraph mode needs Animator enabled; SampleAnimation uses disabled Mecanim + sampled pose.
        if (direct.IsDirectSampling && direct.CurrentMode == CalemOpenWorldDirectClipDriver.LocomotionMode.SampleAnimation)
        {
            TargetAnimator.enabled = false;
        }
        else if (direct.IsDirectSampling && direct.CurrentMode == CalemOpenWorldDirectClipDriver.LocomotionMode.PlayableGraph)
        {
            TargetAnimator.enabled = true;
        }

        LogBootstrapOncePerSession("OK", "Bootstrap complete. Direct sampling=" + direct.IsDirectSampling + ", mode=" + direct.CurrentMode);
    }

    void StartDeferredRigSearchIfNeeded()
    {
        if (!Application.isPlaying)
            return;
        if (_deferredRigSearchRunning)
            return;
        if (TargetAnimator != null)
            return;
        _deferredRigSearchRunning = true;
        _deferredRigCoroutine = StartCoroutine(DeferredRigSearchRoutine());
    }

    IEnumerator DeferredRigSearchRoutine()
    {
        for (int f = 0; f < _rigSearchMaxFrames; f++)
        {
            if (this == null)
                yield break;
            if (TargetAnimator == null)
            {
                // From frame 1, also use scene query — hierarchy search can be empty the same few frames a nested rig appears.
                Transform vis = transform.Find("PlayerAvatarVisual");
                if (vis != null)
                    TargetAnimator = vis.GetComponent<Animator>() ?? vis.GetComponentInChildren<Animator>(true);
                if (TargetAnimator == null)
                {
                    bool useScene = f >= 1;
                    TargetAnimator = OpenWorldRigAnimators.FindRigAnimator(transform, useScene);
                }
            }
            if (TargetAnimator != null)
            {
                _deferredRigCoroutine = null;
                _deferredRigSearchRunning = false;
                ApplyControllerIfReady();
                yield break;
            }
            yield return null;
        }
        _deferredRigCoroutine = null;
        _deferredRigSearchRunning = false;
        if (TargetAnimator == null)
        {
            LogBootstrapOncePerSession(
                "SKIP",
                "No child Animator after " + _rigSearchMaxFrames + " frames. " +
                "If the model is spawned at runtime, ensure OpenWorldEncounterManager is in the scene and " +
                "Resources path loads (see Console for Missing avatar). Otherwise add a rig under the player, " +
                "or assign TargetAnimator.");
        }
    }

    static string s_bootstrapKey;

    void LogBootstrapOncePerSession(string result, string detail)
    {
        if (!Application.isPlaying)
            return;
        // One line per (scene load + object) so the Console has a line you can search: CalemOW-Bootstrap-STATUS
        string key = result + name + GetInstanceID();
        if (key == s_bootstrapKey)
            return;
        s_bootstrapKey = key;
        Debug.Log(
            "[CalemOW-Bootstrap-STATUS] " + result + " | " + name + " | " + detail,
            this);
    }

    RuntimeAnimatorController BuildRuntimeControllerWithoutInvalidEvents(RuntimeAnimatorController source)
    {
        AnimatorOverrideController overrideController = new AnimatorOverrideController(source);
        AnimationClip[] originalClips = source.animationClips;
        AnimationClip[] sanitizedClips = new AnimationClip[originalClips.Length];

        for (int i = 0; i < originalClips.Length; i++)
        {
            AnimationClip original = originalClips[i];
            if (original == null)
                continue;

            AnimationClip clone = Instantiate(original);
            clone.name = original.name + "_Runtime";
            RemoveEmptyEvents(clone);
            sanitizedClips[i] = clone;
        }

        overrideController.ApplyOverrides(BuildPairList(originalClips, sanitizedClips));
        return overrideController;
    }

    static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>> BuildPairList(
        AnimationClip[] originalClips,
        AnimationClip[] overrideClips)
    {
        var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>(originalClips.Length);
        for (int i = 0; i < originalClips.Length; i++)
        {
            AnimationClip original = originalClips[i];
            AnimationClip replacement = overrideClips[i] != null ? overrideClips[i] : original;
            if (original != null)
                pairs.Add(new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(original, replacement));
        }

        return pairs;
    }

    static void RemoveEmptyEvents(AnimationClip clip)
    {
        AnimationEvent[] events = clip.events;
        if (events == null || events.Length == 0)
            return;

        int keptCount = 0;
        for (int i = 0; i < events.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(events[i].functionName))
                keptCount++;
        }

        if (keptCount == events.Length)
            return;

        AnimationEvent[] filtered = new AnimationEvent[keptCount];
        int index = 0;
        for (int i = 0; i < events.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(events[i].functionName))
                filtered[index++] = events[i];
        }

        clip.events = filtered;
    }
}
