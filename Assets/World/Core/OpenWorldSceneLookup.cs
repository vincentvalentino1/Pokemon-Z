using UnityEngine;
using UnityEngine.SceneManagement;

public static class OpenWorldSceneLookup
{
    public static bool TryGetPlayerTransform(out Transform playerTransform)
    {
        playerTransform = null;

        OpenWorldSceneController activeScene = OpenWorldSceneController.Active;
        if (activeScene != null && activeScene.PlayerTransform != null)
        {
            playerTransform = activeScene.PlayerTransform;
            return true;
        }

        if (TryGetPlayerObject(out GameObject playerObject))
        {
            playerTransform = playerObject.transform;
            return true;
        }

        return false;
    }

    public static bool TryGetPlayerController(out OpenWorldPlayerController controller)
    {
        controller = null;

        OpenWorldSceneController activeScene = OpenWorldSceneController.Active;
        if (activeScene != null && activeScene.PlayerController != null)
        {
            controller = activeScene.PlayerController;
            return true;
        }

        OpenWorldPlayerController[] controllers = Object.FindObjectsByType<OpenWorldPlayerController>(FindObjectsSortMode.None);
        Scene active = SceneManager.GetActiveScene();
        for (int i = 0; i < controllers.Length; i++)
        {
            OpenWorldPlayerController candidate = controllers[i];
            if (candidate == null || candidate.gameObject.scene != active)
                continue;

            controller = candidate;
            return true;
        }

        return false;
    }

    public static bool TryGetPlayerObject(out GameObject playerObject)
    {
        playerObject = null;
        Scene active = SceneManager.GetActiveScene();

        try
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.scene == active)
            {
                playerObject = tagged;
                return true;
            }
        }
        catch (UnityException)
        {
        }

        if (TryGetPlayerController(out OpenWorldPlayerController controller) && controller != null)
        {
            playerObject = controller.gameObject;
            return true;
        }

        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject.scene != active)
                continue;

            if (candidate.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                playerObject = candidate.gameObject;
                return true;
            }
        }

        return false;
    }

    public static OpenWorldPlayerStart FindSpawnPoint(string spawnPointId = "", bool includeDefaultFallback = true)
    {
        OpenWorldPlayerStart[] starts = FindSpawnPointsInActiveScene();
        if (starts.Length == 0)
            return null;

        string normalizedId = NormalizeSpawnPointId(spawnPointId);
        if (!string.IsNullOrWhiteSpace(normalizedId))
        {
            for (int i = 0; i < starts.Length; i++)
            {
                OpenWorldPlayerStart start = starts[i];
                if (start != null && start.NormalizedSpawnPointId == normalizedId)
                    return start;
            }
        }

        if (includeDefaultFallback)
        {
            for (int i = 0; i < starts.Length; i++)
            {
                OpenWorldPlayerStart start = starts[i];
                if (start != null && start.UseAsDefaultSpawn)
                    return start;
            }
        }

        return starts[0];
    }

    public static OpenWorldPlayerStart[] FindSpawnPointsInActiveScene()
    {
        OpenWorldPlayerStart[] all = Object.FindObjectsByType<OpenWorldPlayerStart>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
            return System.Array.Empty<OpenWorldPlayerStart>();

        Scene active = SceneManager.GetActiveScene();
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == active)
                count++;
        }

        if (count == 0)
            return System.Array.Empty<OpenWorldPlayerStart>();

        OpenWorldPlayerStart[] filtered = new OpenWorldPlayerStart[count];
        int index = 0;
        for (int i = 0; i < all.Length; i++)
        {
            OpenWorldPlayerStart start = all[i];
            if (start == null || start.gameObject.scene != active)
                continue;

            filtered[index++] = start;
        }

        return filtered;
    }

    public static string NormalizeSpawnPointId(string spawnPointId)
    {
        return string.IsNullOrWhiteSpace(spawnPointId)
            ? string.Empty
            : spawnPointId.Trim().ToLowerInvariant();
    }
}
