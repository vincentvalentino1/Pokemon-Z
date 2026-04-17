using UnityEngine;
using UnityEditor;

public class BattleSceneSetup : EditorWindow
{
    [MenuItem("Tools/Battle/Setup Battle Camera and Spawns")]
    static void Setup()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Error", "No Main Camera found in scene.", "OK");
            return;
        }

        PokemonSpawner spawner = Object.FindObjectOfType<PokemonSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Error", "No PokemonSpawner found in scene.", "OK");
            return;
        }

        Undo.RecordObject(cam.transform, "Setup Battle Camera");
        Undo.RecordObject(cam, "Setup Battle Camera");
        Undo.RecordObject(spawner.transform, "Setup Spawner");
        if (spawner.PlayerSpawnPoint != null)
            Undo.RecordObject(spawner.PlayerSpawnPoint, "Setup Player Spawn");
        if (spawner.EnemySpawnPoint != null)
            Undo.RecordObject(spawner.EnemySpawnPoint, "Setup Enemy Spawn");

        // ── Sample terrain to find ground level ──
        Terrain terrain = Object.FindObjectOfType<Terrain>();
        float groundY = 0f;
        Vector3 samplePoint = new Vector3(3f, 0f, 5f);
        if (terrain != null)
        {
            groundY = terrain.SampleHeight(samplePoint) + terrain.transform.position.y;
        }
        Debug.Log($"[BattleSceneSetup] Ground Y at center: {groundY:F2}");

        // ═══════════════════════════════════════
        //  CLASSIC POKEMON LAYOUT
        //
        //  Screen view:
        //           [Pikachu] ← top-right
        //              ↙
        //           ↗
        //  [Bulbasaur]        ← bottom-left
        //
        //  Camera is behind-left, elevated
        // ═══════════════════════════════════════

        Vector3 playerPos = new Vector3(0f, groundY, 2f);
        Vector3 enemyPos  = new Vector3(6f, groundY, 9f);

        // ── Spawner at origin ──
        spawner.transform.position = Vector3.zero;
        spawner.transform.rotation = Quaternion.identity;
        spawner.transform.localScale = Vector3.one;

        // ── Player: faces enemy ──
        if (spawner.PlayerSpawnPoint != null)
        {
            spawner.PlayerSpawnPoint.position = playerPos;
            Vector3 dir = enemyPos - playerPos;
            dir.y = 0;
            spawner.PlayerSpawnPoint.rotation = Quaternion.LookRotation(dir);
        }

        // ── Enemy: faces player ──
        if (spawner.EnemySpawnPoint != null)
        {
            spawner.EnemySpawnPoint.position = enemyPos;
            Vector3 dir = playerPos - enemyPos;
            dir.y = 0;
            spawner.EnemySpawnPoint.rotation = Quaternion.LookRotation(dir);
        }

        // ── Camera: offset to the right of the battle axis ──
        //    so Bulbasaur appears bottom-LEFT and Pikachu top-RIGHT on screen
        Vector3 midpoint = (playerPos + enemyPos) * 0.5f;

        Vector3 battleDir = (enemyPos - playerPos).normalized;
        Vector3 rightOfAxis = Vector3.Cross(Vector3.up, battleDir).normalized;

        Vector3 camPos = playerPos
            - battleDir * 10f       // behind the player
            + Vector3.up * 7f       // elevated
            + rightOfAxis * 5f;     // offset right → pushes player to screen-left

        cam.transform.position = camPos;
        cam.transform.LookAt(midpoint + Vector3.up * 0.3f);
        cam.fieldOfView = 45;
        cam.nearClipPlane = 0.1f;

        // ── Warm directional light ──
        foreach (Light light in Object.FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                Undo.RecordObject(light.transform, "Setup Light");
                Undo.RecordObject(light, "Setup Light");
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 1.3f;
                light.shadows = LightShadows.Soft;
                break;
            }
        }

        EditorUtility.SetDirty(cam.gameObject);
        EditorUtility.SetDirty(spawner.gameObject);
        if (spawner.PlayerSpawnPoint != null)
            EditorUtility.SetDirty(spawner.PlayerSpawnPoint.gameObject);
        if (spawner.EnemySpawnPoint != null)
            EditorUtility.SetDirty(spawner.EnemySpawnPoint.gameObject);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[BattleSceneSetup] Player: {playerPos}, Enemy: {enemyPos}, Camera: {cam.transform.position}");
        EditorUtility.DisplayDialog("Done",
            $"Battle scene configured!\n\n" +
            $"Ground Y: {groundY:F2}\n" +
            $"Player: bottom-left at ({playerPos.x:F0}, {playerPos.z:F0})\n" +
            $"Enemy: top-right at ({enemyPos.x:F0}, {enemyPos.z:F0})\n" +
            $"Camera: elevated 3/4 view\n\n" +
            "Save the scene (Cmd+S / Ctrl+S).", "OK");
    }
}
