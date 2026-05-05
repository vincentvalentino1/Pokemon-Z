using UnityEngine;

/// <summary>
/// Finds the Animator that drives the visible character mesh (not a duplicate on the movement root).
/// </summary>
public static class OpenWorldRigAnimators
{
    public static Animator FindRigAnimator(Transform root)
    {
        return FindRigAnimator(root, false);
    }

    /// <param name="includeSceneSearchFallback">When the hierarchy search returns no Animators, search all
    /// active/inactive <see cref="Animator"/>s in loaded scenes and pick one under <paramref name="root"/>
    /// (workaround: nested prefab not visible to <see cref="Component.GetComponentsInChildren{T}"/> in the same
    /// frame as parent Awake in some cases).</param>
    public static Animator FindRigAnimator(Transform root, bool includeSceneSearchFallback)
    {
        if (root == null)
            return null;

        Animator found = FindRigAnimatorFromHierarchy(root);
        if (found != null)
            return found;

        if (!includeSceneSearchFallback)
            return null;

        return FindRigAnimatorBySceneQuery(root);
    }

    static Animator FindRigAnimatorFromHierarchy(Transform root)
    {
        if (root == null)
            return null;

        // Runtime-spawned visual — always prefer this so we never bind Mecanim to the wrong child Animator.
        Transform visual = root.Find("PlayerAvatarVisual");
        if (visual != null)
        {
            Animator onVisual = visual.GetComponent<Animator>();
            if (onVisual != null && onVisual.transform != root)
                return onVisual;
            onVisual = visual.GetComponentInChildren<Animator>(true);
            if (onVisual != null && onVisual.transform != root)
                return onVisual;
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        if (animators != null)
        {
            Animator preferSmr = null;
            for (int i = 0; i < animators.Length; i++)
            {
                Animator a = animators[i];
                if (a == null || a.transform == root)
                    continue;
                if (a.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    preferSmr = a;
                    break;
                }
            }

            if (preferSmr != null)
                return preferSmr;

            for (int i = 0; i < animators.Length; i++)
            {
                Animator a = animators[i];
                if (a == null || a.transform == root)
                    continue;
                return a;
            }

            if (animators.Length > 0)
                return animators[0];
        }

        SkinnedMeshRenderer smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            Animator fromMesh = smr.GetComponentInParent<Animator>(true);
            if (fromMesh != null)
                return fromMesh;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == root)
                continue;
            if (t.name == null)
                continue;
            if (t.name.IndexOf("Calem", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            Animator named = t.GetComponent<Animator>();
            if (named != null)
                return named;
        }

        return null;
    }

    static Animator FindRigAnimatorBySceneQuery(Transform root)
    {
        Animator[] all = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null)
            return null;

        for (int i = 0; i < all.Length; i++)
        {
            Animator a = all[i];
            if (a == null)
                continue;
            if (a.transform == root)
                continue;
            if (a.transform.IsChildOf(root))
                return a;
        }

        for (int i = 0; i < all.Length; i++)
        {
            Animator a = all[i];
            if (a != null && a.transform == root)
                return a;
        }

        return null;
    }
}
