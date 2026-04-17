#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility: finds every FBX inside Assets/Resources/Pokemon/{Name}/
/// and creates a prefab at Assets/Resources/Pokemon/{Name}.prefab
/// so that Resources.Load("Pokemon/{Name}") works at runtime.
///
/// Usage: menu bar -> Tools -> Build Pokemon Prefabs
/// </summary>
public static class PokemonPrefabBuilder
{
    [MenuItem("Tools/Build Pokemon Prefabs")]
    public static void BuildAll()
    {
        string root = "Assets/Resources/Pokemon";

        if (!AssetDatabase.IsValidFolder(root))
        {
            Debug.LogError($"[PrefabBuilder] Folder not found: {root}");
            return;
        }

        string[] subfolders = AssetDatabase.GetSubFolders(root);
        int created = 0;
        int skipped = 0;

        foreach (string folder in subfolders)
        {
            string pokemonName = Path.GetFileName(folder);
            string prefabPath = $"{root}/{pokemonName}.prefab";

            if (File.Exists(prefabPath))
            {
                Debug.Log($"[PrefabBuilder] Skipping {pokemonName} -- prefab already exists");
                skipped++;
                continue;
            }

            // Look for an FBX in the subfolder
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { folder });

            if (fbxGuids.Length == 0)
            {
                Debug.LogWarning($"[PrefabBuilder] No model found in {folder}, skipping");
                continue;
            }

            // Use the first model found (typically the main FBX sharing the pokemon's name)
            string fbxPath = null;
            foreach (string guid in fbxGuids)
            {
                string candidate = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(candidate).ToLowerInvariant();
                if (ext == ".fbx" || ext == ".dae")
                {
                    // Prefer the one named after the pokemon
                    if (Path.GetFileNameWithoutExtension(candidate) == pokemonName)
                    {
                        fbxPath = candidate;
                        break;
                    }
                    if (fbxPath == null)
                        fbxPath = candidate;
                }
            }

            if (fbxPath == null)
            {
                Debug.LogWarning($"[PrefabBuilder] No FBX/DAE found in {folder}, skipping");
                continue;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (modelAsset == null)
            {
                Debug.LogWarning($"[PrefabBuilder] Could not load model at {fbxPath}");
                continue;
            }

            // Instantiate into the scene temporarily so we can configure it
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = pokemonName;

            // Attach Animator controller if one exists in the same folder
            string controllerPath = $"{folder}/{pokemonName}.controller";
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();

            if (controller != null)
                animator.runtimeAnimatorController = controller;

            // Save as a new prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            if (prefab != null)
            {
                Debug.Log($"[PrefabBuilder] Created prefab: {prefabPath}");
                created++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[PrefabBuilder] Done -- {created} created, {skipped} skipped");
    }
}
#endif
