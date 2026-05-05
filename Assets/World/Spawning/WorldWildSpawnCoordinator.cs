using UnityEngine;

/// <summary>
/// At most one overworld wild across all <see cref="WorldSpawnArea"/> instances
/// with <see cref="WorldSpawnArea.ParticipateInGlobalWildCap"/> enabled.
/// </summary>
public static class WorldWildSpawnCoordinator
{
    static GameObject _activeWild;

    public static bool CanSpawnWild() => _activeWild == null;

    public static void Register(GameObject wildInstance)
    {
        if (wildInstance == null)
            return;

        _activeWild = wildInstance;
        if (wildInstance.GetComponent<WorldWildSpawnRegistry>() == null)
            wildInstance.AddComponent<WorldWildSpawnRegistry>();
    }

    public static void Release(GameObject wildInstance)
    {
        if (_activeWild == wildInstance)
            _activeWild = null;
    }
}

/// <summary>
/// Clears the global wild slot when this overworld Pokémon is destroyed (battle, despawn, etc.).
/// </summary>
public class WorldWildSpawnRegistry : MonoBehaviour
{
    void OnDestroy()
    {
        WorldWildSpawnCoordinator.Release(gameObject);
    }
}
