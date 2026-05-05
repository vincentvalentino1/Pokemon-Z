using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class BodyTypeAnimationBuilder : EditorWindow
{
    static string AnimFolder => GameContentAssetPaths.AnimationResourcesFolder;
    static string BaseControllerPath => GameContentAssetPaths.AnimationResourcesFolder + "/Base Pokemon Controller.controller";

    // placeholder clip names used in the base controller
    const string IdleClipName    = "Idle";
    const string AttackClipName  = "Basic Attack";
    const string HitClipName     = "Take Hit";

    [MenuItem("Tools/Battle/Build Body-Type Animations")]
    static void Open() => GetWindow<BodyTypeAnimationBuilder>("Body-Type Animations").Show();

    void OnGUI()
    {
        GUILayout.Label("Body-Type Animation Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates idle/attack/hit animation clips for each PokemonBodyType,\n" +
            "creates AnimatorOverrideControllers, assigns clips to the base controller,\n" +
            "and wires BodyType onto Bulbasaur & Pikachu assets.", MessageType.Info);

        if (GUILayout.Button("Generate All", GUILayout.Height(40)))
            GenerateAll();
    }

    static void GenerateAll()
    {
        GameContentAssetPaths.EnsureFolderExists(AnimFolder);

        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        if (baseController == null)
        {
            EditorUtility.DisplayDialog("Error", $"Base controller not found at:\n{BaseControllerPath}", "OK");
            return;
        }

        var bodyTypes = System.Enum.GetValues(typeof(PokemonBodyType));
        int total = bodyTypes.Length;
        int done = 0;

        foreach (PokemonBodyType bt in bodyTypes)
        {
            EditorUtility.DisplayProgressBar("Building Animations", bt.ToString(), (float)done / total);

            AnimationClip idleClip   = BuildIdleClip(bt);
            AnimationClip attackClip = BuildAttackClip(bt);
            AnimationClip hitClip    = BuildHitClip(bt);

            BuildOverrideController(bt, baseController, idleClip, attackClip, hitClip);
            done++;
        }

        AssignClipsToBaseController(baseController);

        WirePokemonAssets();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayDialog("Done",
            $"Generated animations for {total} body types.\n\n" +
            "Each body type now has:\n" +
            "  - Idle / Attack / Hit .anim clips\n" +
            "  - An AnimatorOverrideController\n\n" +
            "Bulbasaur → Quadruped, Pikachu → Biped.", "OK");
    }

    // ═══════════════════════════════════════════════════
    //  IDLE CLIPS — per body type
    // ═══════════════════════════════════════════════════

    static AnimationClip BuildIdleClip(PokemonBodyType bt)
    {
        string clipName = $"{bt}_Battle_Idle";
        string path = $"{AnimFolder}/{clipName}.anim";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.name = clipName;
        clip.frameRate = 60;
        clip.ClearCurves();

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        switch (bt)
        {
            case PokemonBodyType.Quadruped:
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.04f, 1.2f));
                AddCurve(clip, "localScale.x", BreatheCurve(1f, 1.015f, 1.2f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.985f, 1.2f));
                AddCurve(clip, "localScale.z", BreatheCurve(1f, 1.015f, 1.2f));
                break;

            case PokemonBodyType.Biped:
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.06f, 1.0f));
                AddCurve(clip, "localPosition.x", BreatheCurve(0f, 0.015f, 2.0f));
                AddCurve(clip, "localScale.x", BreatheCurve(1f, 1.02f, 1.0f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.98f, 1.0f));
                AddCurve(clip, "localScale.z", BreatheCurve(1f, 1.01f, 1.0f));
                break;

            case PokemonBodyType.Serpentine:
                AddCurve(clip, "localPosition.x", BreatheCurve(0f, 0.04f, 0.8f));
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.02f, 1.6f));
                AddCurve(clip, "localEulerAnglesRaw.z", BreatheCurve(0f, 2f, 0.8f));
                break;

            case PokemonBodyType.Winged:
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.1f, 0.9f));
                AddCurve(clip, "localScale.x", BreatheCurve(1f, 1.03f, 0.9f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.97f, 0.9f));
                AddCurve(clip, "localScale.z", BreatheCurve(1f, 1.03f, 0.9f));
                break;

            case PokemonBodyType.Amorphous:
                AddCurve(clip, "localScale.x", BreatheCurve(1f, 1.04f, 1.5f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.96f, 1.5f));
                AddCurve(clip, "localScale.z", BreatheCurve(1f, 1.04f, 1.5f));
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.03f, 0.75f));
                break;

            case PokemonBodyType.Humanoid:
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.025f, 1.3f));
                AddCurve(clip, "localScale.x", BreatheCurve(1f, 1.008f, 1.3f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.992f, 1.3f));
                AddCurve(clip, "localScale.z", BreatheCurve(1f, 1.008f, 1.3f));
                break;

            case PokemonBodyType.Fish:
                AddCurve(clip, "localPosition.y", BreatheCurve(0f, 0.07f, 1.1f));
                AddCurve(clip, "localEulerAnglesRaw.z", BreatheCurve(0f, 3f, 0.55f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.97f, 1.1f));
                break;

            case PokemonBodyType.HeadBody:
                AddCurve(clip, "localPosition.y", BounceCurve(0f, 0.05f, 1.4f));
                AddCurve(clip, "localScale.x", BreatheCurve(1f, 1.025f, 1.4f));
                AddCurve(clip, "localScale.y", BreatheCurve(1f, 0.975f, 1.4f));
                AddCurve(clip, "localScale.z", BreatheCurve(1f, 1.025f, 1.4f));
                break;
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ═══════════════════════════════════════════════════
    //  ATTACK CLIPS — per body type
    // ═══════════════════════════════════════════════════

    static AnimationClip BuildAttackClip(PokemonBodyType bt)
    {
        string clipName = $"{bt}_Battle_Attack";
        string path = $"{AnimFolder}/{clipName}.anim";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.name = clipName;
        clip.frameRate = 60;
        clip.ClearCurves();

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        float duration = 0.5f;

        Keyframe[] posY = new Keyframe[]
        {
            new Keyframe(0f, 0f),
            new Keyframe(duration * 0.15f, -0.03f),    // wind up
            new Keyframe(duration * 0.35f, 0.08f),     // lunge forward
            new Keyframe(duration * 0.55f, 0.1f),      // peak
            new Keyframe(duration, 0f)                  // return
        };

        Keyframe[] scaleY = new Keyframe[]
        {
            new Keyframe(0f, 1f),
            new Keyframe(duration * 0.15f, 1.05f),     // crouch
            new Keyframe(duration * 0.35f, 0.92f),     // stretch
            new Keyframe(duration * 0.55f, 0.95f),
            new Keyframe(duration, 1f)
        };

        Keyframe[] scaleX = new Keyframe[]
        {
            new Keyframe(0f, 1f),
            new Keyframe(duration * 0.15f, 0.95f),
            new Keyframe(duration * 0.35f, 1.1f),
            new Keyframe(duration * 0.55f, 1.05f),
            new Keyframe(duration, 1f)
        };

        float lungeMultiplier = bt switch
        {
            PokemonBodyType.Quadruped  => 1.0f,
            PokemonBodyType.Biped      => 1.2f,
            PokemonBodyType.Serpentine  => 0.8f,
            PokemonBodyType.Winged     => 1.5f,
            PokemonBodyType.Amorphous  => 0.6f,
            PokemonBodyType.Humanoid   => 1.1f,
            PokemonBodyType.Fish       => 0.7f,
            PokemonBodyType.HeadBody   => 1.3f,
            _ => 1f
        };

        for (int i = 0; i < posY.Length; i++)
            posY[i].value *= lungeMultiplier;

        AddCurve(clip, "localPosition.y", new AnimationCurve(posY));
        AddCurve(clip, "localScale.x", new AnimationCurve(scaleX));
        AddCurve(clip, "localScale.y", new AnimationCurve(scaleY));
        AddCurve(clip, "localScale.z", new AnimationCurve(scaleX));

        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ═══════════════════════════════════════════════════
    //  HIT CLIPS — per body type
    // ═══════════════════════════════════════════════════

    static AnimationClip BuildHitClip(PokemonBodyType bt)
    {
        string clipName = $"{bt}_Battle_Hit";
        string path = $"{AnimFolder}/{clipName}.anim";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.name = clipName;
        clip.frameRate = 60;
        clip.ClearCurves();

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        float duration = 0.4f;

        Keyframe[] posY = new Keyframe[]
        {
            new Keyframe(0f, 0f),
            new Keyframe(duration * 0.1f, -0.04f),     // impact dip
            new Keyframe(duration * 0.3f, 0.03f),      // recoil up
            new Keyframe(duration * 0.6f, -0.01f),     // settle
            new Keyframe(duration, 0f)
        };

        Keyframe[] scaleX = new Keyframe[]
        {
            new Keyframe(0f, 1f),
            new Keyframe(duration * 0.1f, 1.08f),      // squash on impact
            new Keyframe(duration * 0.3f, 0.95f),
            new Keyframe(duration * 0.6f, 1.02f),
            new Keyframe(duration, 1f)
        };

        Keyframe[] scaleY = new Keyframe[]
        {
            new Keyframe(0f, 1f),
            new Keyframe(duration * 0.1f, 0.92f),      // squash
            new Keyframe(duration * 0.3f, 1.06f),      // stretch recoil
            new Keyframe(duration * 0.6f, 0.98f),
            new Keyframe(duration, 1f)
        };

        AddCurve(clip, "localPosition.y", new AnimationCurve(posY));
        AddCurve(clip, "localScale.x", new AnimationCurve(scaleX));
        AddCurve(clip, "localScale.y", new AnimationCurve(scaleY));
        AddCurve(clip, "localScale.z", new AnimationCurve(scaleX));

        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ═══════════════════════════════════════════════════
    //  OVERRIDE CONTROLLERS
    // ═══════════════════════════════════════════════════

    static void BuildOverrideController(PokemonBodyType bt,
        AnimatorController baseCtrl,
        AnimationClip idle, AnimationClip attack, AnimationClip hit)
    {
        string ocName = $"{bt}Override";
        string ocPath = $"{AnimFolder}/{ocName}.overrideController";

        var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ocPath);
        if (oc == null)
        {
            oc = new AnimatorOverrideController(baseCtrl);
            AssetDatabase.CreateAsset(oc, ocPath);
        }
        else
        {
            oc.runtimeAnimatorController = baseCtrl;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        oc.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            var orig = overrides[i].Key;
            if (orig == null) continue;

            if (orig.name.Contains("Idle"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, idle);
            else if (orig.name.Contains("Attack"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, attack);
            else if (orig.name.Contains("Hit"))
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(orig, hit);
        }

        oc.ApplyOverrides(overrides);
        EditorUtility.SetDirty(oc);
    }

    // ═══════════════════════════════════════════════════
    //  ASSIGN PLACEHOLDER CLIPS TO BASE CONTROLLER
    // ═══════════════════════════════════════════════════

    static void AssignClipsToBaseController(AnimatorController ctrl)
    {
        var idlePath   = $"{AnimFolder}/Quadruped_Battle_Idle.anim";
        var attackPath = $"{AnimFolder}/Quadruped_Battle_Attack.anim";
        var hitPath    = $"{AnimFolder}/Quadruped_Battle_Hit.anim";

        var idleClip   = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
        var attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(attackPath);
        var hitClip    = AssetDatabase.LoadAssetAtPath<AnimationClip>(hitPath);

        foreach (var layer in ctrl.layers)
        {
            foreach (var cs in layer.stateMachine.states)
            {
                var state = cs.state;
                if (state.motion != null) continue;

                if (state.name == "Idle" && idleClip != null)
                    state.motion = idleClip;
                else if (state.name == "Basic Attack" && attackClip != null)
                    state.motion = attackClip;
                else if (state.name == "Take Hit" && hitClip != null)
                    state.motion = hitClip;
            }
        }

        EditorUtility.SetDirty(ctrl);
    }

    // ═══════════════════════════════════════════════════
    //  WIRE POKEMON ASSETS
    // ═══════════════════════════════════════════════════

    static void WirePokemonAssets()
    {
        var mapping = new Dictionary<string, PokemonBodyType>
        {
            { "Bulbasaur", PokemonBodyType.Quadruped },
            { "Pikachu",   PokemonBodyType.Biped }
        };

        string[] guids = AssetDatabase.FindAssets("t:PokemonData", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<PokemonData>(assetPath);
            if (data == null) continue;

            if (mapping.TryGetValue(data.PokemonName, out PokemonBodyType bt))
            {
                data.BodyType = bt;
                EditorUtility.SetDirty(data);
                Debug.Log($"[BodyTypeAnimBuilder] {data.PokemonName} → {bt}");
            }
        }
    }

    // ═══════════════════════════════════════════════════
    //  CURVE HELPERS
    // ═══════════════════════════════════════════════════

    static void AddCurve(AnimationClip clip, string propertyName, AnimationCurve curve)
    {
        clip.SetCurve("", typeof(Transform), propertyName, curve);
    }

    /// Smooth sine-like breathe: base → peak → base over `period` seconds
    static AnimationCurve BreatheCurve(float baseVal, float peakVal, float period)
    {
        int steps = 32;
        Keyframe[] keys = new Keyframe[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps * period;
            float sin = Mathf.Sin(t / period * Mathf.PI * 2f);
            float val = Mathf.Lerp(baseVal, peakVal, (sin + 1f) * 0.5f);
            keys[i] = new Keyframe(t, val);
        }
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        return curve;
    }

    /// Sharper bounce: quick up, slow settle
    static AnimationCurve BounceCurve(float baseVal, float peakVal, float period)
    {
        int steps = 32;
        Keyframe[] keys = new Keyframe[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps * period;
            float phase = t / period * Mathf.PI * 2f;
            float sin = Mathf.Abs(Mathf.Sin(phase));
            float val = Mathf.Lerp(baseVal, peakVal, sin);
            keys[i] = new Keyframe(t, val);
        }
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
        return curve;
    }
}
