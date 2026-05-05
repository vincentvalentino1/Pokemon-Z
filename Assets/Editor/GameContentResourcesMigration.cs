#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Text;

/// <summary>
/// One-time (per clone) move of gameplay <c>Resources</c> subtrees into <c>Assets/GameContent/Resources/</c>.
/// Runtime load paths such as <c>Pokemon/Name</c> are unchanged.
/// </summary>
public class GameContentResourcesMigration : EditorWindow
{
    static readonly string[] s_foldersToRelocate =
    {
        "Pokemon",
        "Pokedex",
        "Character",
        "Animation",
        "Behavior",
        "Spawn"
    };

    [MenuItem("Tools/Game Content/Migrate Resources Into GameContent…")]
    static void Open()
    {
        GetWindow<GameContentResourcesMigration>(true, "Migrate Resources", true);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Resources → Game Content", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Moves gameplay folders from Assets/Resources/ into Assets/GameContent/Resources/. " +
            "Runtime Resources.Load paths (e.g. Pokemon/Bulbasaur, Pokedex/PokedexDatabase) stay the same.\n\n" +
            "Commit or back up first. Teammates on the legacy layout can keep working until they pull your migrated tree.",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
        EditorGUILayout.TextArea(BuildPreview(), GUILayout.MinHeight(120f));

        EditorGUILayout.Space(8f);
        GUI.backgroundColor = new Color(1f, 0.75f, 0.5f);
        if (GUILayout.Button("Run migration", GUILayout.Height(36f)))
            RunMigration();
        GUI.backgroundColor = Color.white;
    }

    static string BuildPreview()
    {
        var sb = new StringBuilder();
        foreach (string name in s_foldersToRelocate)
        {
            string from = GameContentAssetPaths.LegacyResourcesRoot + "/" + name;
            string to = GameContentAssetPaths.MigratedResourcesRoot + "/" + name;
            bool src = AssetDatabase.IsValidFolder(from);
            bool dst = AssetDatabase.IsValidFolder(to);
            sb.Append(name);
            sb.Append(": ");
            if (dst)
                sb.AppendLine("(already under GameContent — skipped)");
            else if (src)
                sb.AppendLine(from + " → " + to);
            else
                sb.AppendLine("(no source folder — skipped)");
        }

        return sb.ToString();
    }

    static void RunMigration()
    {
        if (!EditorUtility.DisplayDialog(
                "Migrate Resources",
                "Move gameplay Resources folders into Assets/GameContent/Resources/?\n\n" +
                "This cannot be undone by this tool (use version control to revert).",
                "Migrate",
                "Cancel"))
            return;

        GameContentAssetPaths.EnsureFolderExists(GameContentAssetPaths.GameContentRoot);
        GameContentAssetPaths.EnsureFolderExists(GameContentAssetPaths.MigratedResourcesRoot);

        int moved = 0;
        foreach (string name in s_foldersToRelocate)
        {
            string from = GameContentAssetPaths.LegacyResourcesRoot + "/" + name;
            string to = GameContentAssetPaths.MigratedResourcesRoot + "/" + name;

            if (!AssetDatabase.IsValidFolder(from))
            {
                Debug.Log($"[GameContentMigration] Skip (no source): {from}");
                continue;
            }

            if (AssetDatabase.IsValidFolder(to))
            {
                Debug.LogWarning($"[GameContentMigration] Skip (destination exists): {to}");
                continue;
            }

            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"[GameContentMigration] Move failed: {error}");
            else
            {
                Debug.Log($"[GameContentMigration] Moved {from} → {to}");
                moved++;
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Migration complete",
            moved > 0
                ? $"Moved {moved} folder(s). Editor tools now prefer GameContent/Resources when present."
                : "Nothing was moved (already migrated or missing source folders).",
            "OK");
    }
}
#endif
